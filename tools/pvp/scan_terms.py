#!/usr/bin/env python3
"""Multi-term GBK/UTF-16LE scan over text assets; tagged UTF-8 output.

Usage:
  python tools/pvp/scan_terms.py <out.txt> --terms a,b,c --files f1,f2 --dirs d1,d2
                                  [--ext .lua,.txt] [--context 100] [--max-hits 15]
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path


def scan(path: Path, terms: list[str], context: int, max_hits: int) -> list[str]:
    data = path.read_bytes()
    out: list[str] = []
    for term in terms:
        for enc, needle in (("gbk", term.encode("gb18030")), ("utf16le", term.encode("utf-16le"))):
            if not needle:
                continue
            start = 0
            hits = 0
            while hits < max_hits:
                off = data.find(needle, start)
                if off < 0:
                    break
                s = max(0, off - context)
                e = min(len(data), off + len(needle) + context)
                chunk = data[s:e]
                text = chunk.decode("gb18030" if enc == "gbk" else "utf-16le", errors="replace")
                text = "".join(c if c.isprintable() or c in "\t" else " " for c in text)
                out.append(f"{path}|0x{off:08X}|{enc}|{term}|{text}")
                start = off + 1
                hits += 1
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("out", type=Path)
    ap.add_argument("--terms", required=True)
    ap.add_argument("--files", default="")
    ap.add_argument("--dirs", default="")
    ap.add_argument("--ext", default=".lua,.txt,.ini,.jx3dat,.tab")
    ap.add_argument("--context", type=int, default=100)
    ap.add_argument("--max-hits", type=int, default=15)
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    terms = [t for t in args.terms.split(",") if t]
    exts = tuple(e.strip().lower() for e in args.ext.split(",") if e.strip())
    targets = [Path(p) for p in args.files.split(",") if p]
    for d in [d for d in args.dirs.split(",") if d]:
        dp = Path(d)
        targets += [p for p in dp.rglob("*") if p.is_file() and p.suffix.lower() in exts]
    targets = [p for p in targets if p.is_file()]

    lines: list[str] = []
    for p in targets:
        try:
            lines += scan(p, terms, args.context, args.max_hits)
        except (PermissionError, OSError) as e:
            lines.append(f"# skip {p}: {e}")
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text("\n".join(lines) + ("\n" if lines else ""), encoding="utf-8")
    print(f"{len(lines)} hits over {len(targets)} files -> {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
