# -*- coding: utf-8 -*-
"""Dump 花萝 FBX skeleton (name, parent, bind local TRS) via Playwright viewport."""
from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path

from fbx_actor import ensure_server, load_fbx_actor, web_viewport_url

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "web" / "runtime" / "fbx_skel_bind.json"


def main() -> None:
    actor = load_fbx_actor()
    ensure_server(actor)
    url = web_viewport_url(actor, clip_fbx=None, t=None, playing=False)
    chrome = (
        shutil.which("msedge")
        or r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
        or r"C:\Program Files\Google\Chrome\Application\chrome.exe"
    )
    for cand in (
        r"C:\Program Files\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    ):
        if Path(cand).is_file():
            chrome = cand
            break
    script = ROOT / "web" / "dump_fbx_skel.mjs"
    cmd = [
        "node",
        str(script),
        "--url",
        url,
        "--out",
        str(OUT),
        "--chrome",
        chrome,
        "--timeout",
        "120000",
    ]
    proc = subprocess.run(cmd, cwd=str(ROOT), capture_output=True, text=True, timeout=180)
    print(proc.stdout)
    print(proc.stderr)
    if proc.returncode != 0:
        raise SystemExit(proc.returncode)
    print("WROTE", OUT, OUT.stat().st_size if OUT.is_file() else 0)


if __name__ == "__main__":
    main()
