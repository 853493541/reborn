#!/usr/bin/env python3
"""Find skill/motion/replication table candidates in a VFS file tree.

Scans FolderTree.xml-style listings for paths whose names look like gameplay
tables (skill, motion, rush, move) or Chinese equivalents, so we can extract
the real configs from PakV4 and read dash parameters.
"""
from __future__ import annotations

import argparse
import re
import sys
from collections import Counter
from pathlib import Path

KEYWORDS = [
    "skill", "motion", "rush", "move", "action", "talent", "buff", "aura",
    "技能", "招式", "动作", "位移", "冲锋", "突进", "轻功",
]
PATH_RE = re.compile(r"[^<>\r\n\"]{4,300}")


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("tree", type=Path)
    ap.add_argument("--ext", default=".tab,.txt,.rt,.ini,.xml,.json",
                    help="comma separated extensions to keep")
    ap.add_argument("--out", type=Path)
    args = ap.parse_args(argv)

    exts = tuple(e.strip().lower() for e in args.ext.split(",") if e.strip())
    raw = args.tree.read_bytes()
    for enc in ("gb18030", "utf-8"):
        try:
            text = raw.decode(enc)
            break
        except UnicodeDecodeError:
            continue
    else:
        text = raw.decode("latin1", errors="replace")

    hits: Counter[str] = Counter()
    for m in PATH_RE.finditer(text):
        s = m.group(0)
        low = s.lower()
        if not low.endswith(exts):
            continue
        if not any(k in low or k in s for k in KEYWORDS):
            continue
        hits[s.strip()] += 1

    lines = [f"{count}\t{path}" for path, count in hits.most_common()]
    text_out = "\n".join(lines) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text_out, encoding="utf-8", errors="replace")
        print(f"{len(hits)} candidates -> {args.out}")
    else:
        sys.stdout.write(text_out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
