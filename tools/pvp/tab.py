#!/usr/bin/env python3
"""Read JX3 GBK TSV tables (.tab / UI Case .txt) with header names.

Usage:
  python tools/pvp/tab.py HEADER <table> [--cols a,b] [--match COL=SUBSTR] [--limit N]
  python tools/pvp/tab.py ROWS <table> --cols SkillName,SkillID --match SkillName=龙牙 [--limit 20]
  python tools/pvp/tab.py ATTRS <Attribute.txt>

All tables are decoded as GB18030. `--match` is a case-insensitive substring
test on one column. Output is UTF-8 TSV with a header row.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

TEXT_TAG = re.compile(r'text="([^"]*)"')


def load(path: Path) -> tuple[list[str], list[list[str]]]:
    text = path.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    if not rows:
        return [], []
    return rows[0], rows[1:]


def col_index(header: list[str], name: str) -> int:
    for i, h in enumerate(header):
        if h == name:
            return i
    raise SystemExit(f"column not found: {name}\ncolumns: {', '.join(header)}")


def clean(cell: str) -> str:
    m = TEXT_TAG.search(cell)
    return m.group(1) if m else cell


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("mode", choices=["HEADER", "ROWS", "ATTRS"])
    ap.add_argument("table", type=Path)
    ap.add_argument("--cols", default="")
    ap.add_argument("--match", default="")
    ap.add_argument("--limit", type=int, default=50)
    ap.add_argument("--clean", action="store_true", help="unwrap <text text=...>")
    args = ap.parse_args()

    header, rows = load(args.table)
    if args.mode == "HEADER":
        print(f"# rows={len(rows)} cols={len(header)}")
        for i, h in enumerate(header):
            print(f"{i}\t{h}")
        return 0

    if args.mode == "ATTRS":
        for r in rows:
            rid = r[0]
            val = r[4] if len(r) > 4 else ""
            print(f"{rid}\t{clean(val)}")
        return 0

    cols = [c for c in args.cols.split(",") if c]
    if not cols:
        cols = header
    idxs = [col_index(header, c) for c in cols]
    match_col = match_val = None
    if args.match:
        name, _, match_val = args.match.partition("=")
        match_col = col_index(header, name)
    print("\t".join(cols))
    shown = 0
    for r in rows:
        if match_col is not None:
            cell = r[match_col] if match_col < len(r) else ""
            if match_val.lower() not in cell.lower():
                continue
        out = []
        for i in idxs:
            cell = r[i] if i < len(r) else ""
            out.append(clean(cell) if args.clean else cell)
        print("\t".join(out))
        shown += 1
        if shown >= args.limit:
            break
    print(f"# shown {shown}/{len(rows)}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
