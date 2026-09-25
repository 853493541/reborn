#!/usr/bin/env python3
"""Grep JX3 GB18030 tables (.tab / UI Case .txt) for substrings across all columns.

Usage:
  python tools/pvp/grep_tab.py <table> TERM [TERM...] [--cols a,b] [--limit N] [--any]
  python tools/pvp/grep_tab.py <table> --header

--any   : match if ANY term hits (default: all terms must hit somewhere in the row)
Output: UTF-8 TSV (header row + matching rows), plus a match count on stderr.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path


def load(path: Path) -> tuple[list[str], list[list[str]]]:
    text = path.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    if not rows:
        return [], []
    return rows[0], rows[1:]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser()
    ap.add_argument("table", type=Path)
    ap.add_argument("terms", nargs="*")
    ap.add_argument("--cols", default="")
    ap.add_argument("--limit", type=int, default=200)
    ap.add_argument("--any", action="store_true")
    ap.add_argument("--header", action="store_true")
    ap.add_argument("--id", default="", help="print only these row ids (comma list) in row order")
    ap.add_argument("--out", default="")
    args = ap.parse_args()

    out_lines: list[str] = []
    out = out_lines.append if args.out else print

    header, rows = load(args.table)
    if args.header:
        out(f"# rows={len(rows)} cols={len(header)}")
        for i, h in enumerate(header):
            out(f"{i}\t{h}")
        if args.out:
            Path(args.out).write_text("\n".join(out_lines) + "\n", encoding="utf-8")
        return 0

    cols = [c for c in args.cols.split(",") if c]
    if cols:
        idxs = []
        for c in cols:
            if c not in header:
                raise SystemExit(f"column not found: {c}")
            idxs.append(header.index(c))
    else:
        idxs = list(range(len(header)))

    want_ids = None
    if args.id:
        want_ids = [x for x in args.id.split(",") if x]

    hits = 0
    out("\t".join(header[i] for i in idxs))
    for r in rows:
        if want_ids is not None:
            if (r[0] if r else "") not in want_ids:
                continue
        else:
            blob = "\t".join(r)
            tests = [t.lower() in blob.lower() for t in args.terms]
            ok = any(tests) if args.any else all(tests)
            if not ok:
                continue
        cells = [r[i] if i < len(r) else "" for i in idxs]
        out("\t".join(cells))
        hits += 1
        if hits >= args.limit:
            break
    out(f"# matched {hits} (limit {args.limit})")
    if args.out:
        Path(args.out).write_text("\n".join(out_lines) + "\n", encoding="utf-8")
        print(f"wrote {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
