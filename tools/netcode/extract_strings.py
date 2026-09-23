#!/usr/bin/env python3
"""Extract ASCII / UTF-16LE / GBK strings from a binary with file offsets.

Read-only helper for the JX3 netcode research. Every string is reported with
its file offset so findings in docs/netcode/JX3_NETCODE_RESEARCH.md can cite
exact evidence.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

ASCII_RE = re.compile(rb"[\x20-\x7e]{4,}")
UTF16_RE = re.compile(rb"(?:[\x20-\x7e]\x00){4,}")
UTF16_CJK_RE = re.compile(rb"(?:[\x00-\xff][\x4e-\x9f]){2,}")
GBK_RE = re.compile(rb"(?:[\x81-\xfe][\x40-\x7e\x80-\xfe]){2,}")


def _cjk_count(text: str) -> int:
    return sum(1 for ch in text if "\u4e00" <= ch <= "\u9fff")


def extract(path: Path, min_len: int = 4) -> list[tuple[int, str, str]]:
    data = path.read_bytes()
    out: list[tuple[int, str, str]] = []

    for m in ASCII_RE.finditer(data):
        s = m.group().decode("ascii", "replace")
        if len(s) >= min_len:
            out.append((m.start(), "ascii", s))

    for m in UTF16_RE.finditer(data):
        s = m.group().decode("utf-16le", "replace")
        if len(s) >= min_len:
            out.append((m.start(), "utf16le", s))

    for m in UTF16_CJK_RE.finditer(data):
        s = m.group().decode("utf-16le", "ignore").strip()
        if _cjk_count(s) >= 2:
            out.append((m.start(), "utf16cjk", s))

    for m in GBK_RE.finditer(data):
        s = m.group().decode("gbk", "ignore").strip()
        if _cjk_count(s) >= 2:
            out.append((m.start(), "gbk", s))

    out.sort(key=lambda t: t[0])
    return out


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("binary", type=Path)
    ap.add_argument("-o", "--out", type=Path, help="output TSV (default: stdout)")
    ap.add_argument("--min-len", type=int, default=4)
    args = ap.parse_args(argv)

    if not args.binary.is_file():
        print(f"not a file: {args.binary}", file=sys.stderr)
        return 2

    strings = extract(args.binary, args.min_len)
    lines = [f"0x{off:08X}\t{enc}\t{s}" for off, enc, s in strings]
    text = "\n".join(lines) + ("\n" if lines else "")

    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8", errors="replace")
        print(f"{args.binary.name}: {len(strings)} strings -> {args.out}")
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
