#!/usr/bin/env python3
"""Serve + optional screenshot for the Three.js 花萝 viewport.

Keeps Ani Player chrome intact: open this panel alongside Tk, or dump proof PNGs.
"""
from __future__ import annotations

import argparse
import json
import time
from pathlib import Path

from fbx_actor import DEFAULT_FBX, ensure_server, load_fbx_actor, open_viewport, paint_to


def main() -> None:
    ap = argparse.ArgumentParser(description="F1 花萝 Three.js viewport helper")
    ap.add_argument("--fbx", type=Path, default=DEFAULT_FBX)
    ap.add_argument("--serve", action="store_true", help="Serve forever (open browser)")
    ap.add_argument("--proof", type=Path, help="Write close-up PNG to this path")
    ap.add_argument(
        "--out",
        type=Path,
        default=Path(__file__).resolve().parent / "proof" / "compare" / "aniplayer_f1_hualuo_closeup.png",
    )
    args = ap.parse_args()
    actor = load_fbx_actor(args.fbx)
    port = ensure_server(actor)
    url = actor.viewport_url(closeup=True)
    print(f"serving http://127.0.0.1:{port}/  viewport={url}")
    print(f"fbx={actor.fbx_path} tex={actor.tex_dir} files={actor.meta.get('texture_files')}")

    out = args.proof or (None if args.serve else args.out)
    if out:
        meta = paint_to(actor, out)
        print(json.dumps(meta, indent=2, ensure_ascii=False))

    if args.serve:
        open_viewport(actor)
        print("Ctrl+C to stop")
        try:
            while True:
                time.sleep(3600)
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
