#!/usr/bin/env python3
"""Dump candidate float keyframe runs from extracted .tani files.

GATA motion tags store authored keyframes; this helper finds runs of plausible
float values (times/positions) so we can eyeball dash displacement data.
"""
from __future__ import annotations

import argparse
import math
import struct
from pathlib import Path


def runs_in(data: bytes, min_run: int, limit: float) -> list[tuple[int, int]]:
    out = []
    i = 0
    n = len(data)
    while i < n - 24:
        vals = struct.unpack_from("<6f", data, i)
        if all(math.isfinite(v) and abs(v) < limit for v in vals) and \
                sum(1 for v in vals if abs(v) > 1e-4) >= 4:
            j = i + 24
            count = 6
            while j < n - 4:
                v = struct.unpack_from("<f", data, j)[0]
                if math.isfinite(v) and abs(v) < limit and abs(v) > 1e-4:
                    j += 4
                    count += 1
                else:
                    break
            if count >= min_run:
                out.append((i, count))
                i = j
                continue
        i += 4
    return out


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("paths", nargs="+", type=Path)
    ap.add_argument("--min-run", type=int, default=8)
    ap.add_argument("--limit", type=float, default=2000.0)
    ap.add_argument("--max-runs", type=int, default=12)
    args = ap.parse_args(argv)

    for path in args.paths:
        if path.is_dir():
            files = [f for f in path.iterdir() if f.suffix == ".tani"]
        else:
            files = [path]
        for f in files:
            b = f.read_bytes()
            rs = runs_in(b, args.min_run, args.limit)
            print(f"== {f.name} size={len(b)} runs={len(rs)}")
            for off, count in rs[: args.max_runs]:
                chunk = b[off: off + min(count * 4, 40)]
                vals = struct.unpack_from("<" + "f" * (len(chunk) // 4), chunk)
                print(f"  0x{off:04x} n={count} " + " ".join(f"{v:.2f}" for v in vals))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
