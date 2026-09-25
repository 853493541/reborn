#!/usr/bin/env python3
"""Extract C++ member-name runs around anchor tokens from a PE binary (read-only).

JX3 binaries pack adjacent std::string-ish literals / member-name arrays in .rdata;
scanning for an anchor token and printing a raw window (nulls -> '.') recovers
plausible struct field runs without a full 100 MB string dump.

Usage:
  python member_runs.py <binary> <anchor> [<anchor> ...] [--window 600] [--ctx 32]
                        [--maxhits 40] [-o out.txt] [--gbk]
"""
import argparse
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def print_window(data: bytes, off: int, window: int, gbk: bool) -> str:
    chunk = data[off:off + window]
    if gbk:
        try:
            text = chunk.decode("gbk", errors="replace")
        except Exception:
            text = chunk.decode("latin-1", errors="replace")
    else:
        text = chunk.decode("latin-1", errors="replace")
    out = []
    for ch in text:
        o = ord(ch)
        if ch in "\r\n\t":
            out.append(" ")
        elif o < 0x20 or o == 0x7F:
            out.append(".")
        else:
            out.append(ch)
    return "".join(out)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("binary")
    ap.add_argument("anchors", nargs="+")
    ap.add_argument("--window", type=int, default=600)
    ap.add_argument("--ctx", type=int, default=32)
    ap.add_argument("--maxhits", type=int, default=40)
    ap.add_argument("--gbk", action="store_true")
    ap.add_argument("-o", "--out")
    args = ap.parse_args()

    with open(args.binary, "rb") as fh:
        data = fh.read()

    lines = [f"# member runs from {args.binary}",
             f"# anchors: {args.anchors}  window={args.window} ctx={args.ctx}"]
    for anchor in args.anchors:
        b = anchor.encode("latin-1")
        off = 0
        hits = 0
        while hits < args.maxhits:
            pos = data.find(b, off)
            if pos < 0:
                break
            start = max(0, pos - args.ctx)
            lines.append(f"## {anchor} @ file+0x{pos:08X}")
            lines.append(print_window(data, start, args.window, args.gbk))
            off = pos + 1
            hits += 1
        if hits == 0:
            lines.append(f"## {anchor} @ NOT FOUND")

    text = "\n".join(lines) + "\n"
    if args.out:
        with open(args.out, "w", encoding="utf-8", errors="replace") as fh:
            fh.write(text)
        print(f"wrote {args.out} ({len(text)} bytes)")
    else:
        print(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
