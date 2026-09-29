#!/usr/bin/env python3
"""Scan extracted tani files for keywords (ASCII pinyin/event names and GBK
Chinese effect names). A hit means the tani carries that sound/PSS tag."""
from __future__ import annotations

import argparse
import json
import os
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass


def gata_ani(data: bytes) -> str:
    if data[:4] != b"GATA":
        return ""
    j = data.find(b"\x00", 8)
    try:
        return data[8:j].decode("gb18030", errors="replace") if j > 0 else ""
    except Exception:
        return ""


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dirs", required=True, help="semicolon separated dirs")
    ap.add_argument("--keywords", required=True, help="comma separated (ascii or chinese)")
    ap.add_argument("--json", default="", help="write hits json")
    args = ap.parse_args()

    dirs = [d for d in args.dirs.split(";") if d.strip() and os.path.isdir(d)]
    kws = [k.strip() for k in args.keywords.split(",") if k.strip()]
    kbs = [(k, k.encode("gb18030", errors="ignore")) for k in kws]

    hits = {}
    files = 0
    for root_dir in dirs:
        for r, _d, fs in os.walk(root_dir):
            for fn in fs:
                if not fn.lower().endswith(".tani"):
                    continue
                files += 1
                p = os.path.join(r, fn)
                try:
                    data = open(p, "rb").read()
                except Exception:
                    continue
                for kw, kb in kbs:
                    if not kb:
                        continue
                    i = data.find(kb)
                    if i < 0:
                        continue
                    ctx = data[max(0, i - 60):i + 110].decode("gb18030", errors="replace").replace("\x00", "")
                    hits.setdefault(kw, []).append((fn, gata_ani(data), ctx))
    print(f"scanned files={files} keywords={len(kws)}")
    for kw in kws:
        v = hits.get(kw, [])
        if not v:
            print(f"== {kw}: 0")
            continue
        print(f"== {kw}: {len(v)}")
        for fn, ani, ctx in v[:40]:
            print(f"   {fn}")
            print(f"      ani={ani}")
            print(f"      ctx={ctx[:130]}")
    if args.json:
        json.dump(hits, open(args.json, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
