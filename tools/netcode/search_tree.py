#!/usr/bin/env python3
"""Search a GBK XML/text tree for tokens and print matching lines with context."""
from __future__ import annotations

import argparse
import sys
from pathlib import Path


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("file", type=Path)
    ap.add_argument("terms", nargs="+")
    ap.add_argument("--limit", type=int, default=12)
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    data = args.file.read_bytes().decode("gb18030", errors="replace")
    for term in args.terms:
        hits = []
        start = 0
        while len(hits) < args.limit:
            i = data.find(term, start)
            if i < 0:
                break
            a = max(0, i - 80)
            b = min(len(data), i + 120)
            ctx = data[a:b].replace("\r", " ").replace("\n", " ")
            hits.append(ctx)
            start = i + 1
        print(f"== {term}: {len(hits)} shown")
        for h in hits:
            print("   ", h)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
