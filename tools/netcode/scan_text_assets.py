#!/usr/bin/env python3
"""Scan UI asset trees for CJK gameplay text (skill formulas, scaling).

Searches GBK, UTF-16LE and UTF-8 encodings of a keyword list and prints
file + offset + a short context window. Meant for interface/,.jx3dat/.lua/etc.

Usage:
  python tools/netcode/scan_text_assets.py "C:\\...\\interface" --keywords 攻击力 伤害 加成
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

DEFAULT_KEYWORDS = ["攻击力", "伤害", "加成", "系数", "内功", "外功", "会心", "破防"]
DEFAULT_EXTS = ".jx3dat,.lua,.txt,.ini,.udb,.dat,.jcl,.zh_tw,.json,.xml"
DEFAULT_EXTS_SET = {".jx3dat", ".lua", ".txt", ".ini", ".udb", ".dat", ".jcl", ".zh_tw", ".json", ".xml"}


def contexts(data: bytes, needle: bytes, window: int = 40) -> list[tuple[int, bytes]]:
    out = []
    start = 0
    while True:
        i = data.find(needle, start)
        if i < 0:
            return out
        out.append((i, data[max(0, i - window): i + len(needle) + window]))
        start = i + 1


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("root", type=Path)
    ap.add_argument("--keywords", nargs="*", default=DEFAULT_KEYWORDS)
    ap.add_argument("--max-mb", type=float, default=20.0)
    ap.add_argument("--exts", default=DEFAULT_EXTS)
    ap.add_argument("--limit-files", type=int, default=0)
    ap.add_argument("--out", type=Path)
    args = ap.parse_args(argv)

    exts = {e if e.startswith(".") else "." + e for e in args.exts.split(",") if e}
    max_bytes = int(args.max_mb * 1024 * 1024)
    files = [p for p in args.root.rglob("*") if p.is_file() and p.suffix.lower() in exts]
    if args.limit_files:
        files = files[: args.limit_files]
    print(f"scanning {len(files)} files under {args.root}", file=sys.stderr)

    lines: list[str] = []
    scanned = skipped = 0
    for path in files:
        try:
            if path.stat().st_size > max_bytes:
                skipped += 1
                continue
            data = path.read_bytes()
        except OSError:
            skipped += 1
            continue
        scanned += 1
        for kw in args.keywords:
            for enc in ("gbk", "utf-16le", "utf-8"):
                try:
                    needle = kw.encode(enc)
                except UnicodeEncodeError:
                    continue
                if not needle:
                    continue
                for off, ctx in contexts(data, needle):
                    try:
                        text = ctx.decode("gbk", errors="replace") if enc == "gbk" else ctx.decode(enc, "replace")
                    except Exception:  # noqa: BLE001
                        text = ctx.decode("latin1", "replace")
                    text = "".join(ch if ch.isprintable() else "." for ch in text)
                    lines.append(f"{path}\t0x{off:X}\t{enc}\t{kw}\t{text}")
                    break  # one hit per encoding per keyword per file is enough
    out_text = "\n".join(lines) + ("\n" if lines else "")
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(out_text, encoding="utf-8", errors="replace")
        print(f"{len(lines)} hits ({scanned} scanned, {skipped} skipped) -> {args.out}")
    else:
        sys.stdout.write(out_text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
