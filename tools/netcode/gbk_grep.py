#!/usr/bin/env python3
"""Search binary/text files for a Chinese (GBK or UTF-16LE) term with context.

Chinese strings in JX3 binaries are usually GBK; UI data may be UTF-16LE.
Prints file, offset, encoding and decoded context so findings are citable.

Usage:
  python tools/netcode/gbk_grep.py 绝境 --dir "C:\\SeasunGame\\Game\\JX3\\bin\\zhcn_hd" --ext .dll,.exe,.dat,.txt,.lua,.ini
  python tools/netcode/gbk_grep.py 绝境 somefile.bin --context 120
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

DEFAULT_EXTS = (".dll", ".exe", ".dat", ".txt", ".lua", ".ini", ".xml", ".rt", ".tab", ".json")


def decode_context(data: bytes, off: int, length: int, encoding: str, context: int) -> str:
    start = max(0, off - context)
    end = min(len(data), off + length + context)
    chunk = data[start:end]
    if encoding == "gbk":
        text = chunk.decode("gb18030", errors="replace")
    else:
        try:
            text = chunk.decode("utf-16le", errors="replace")
        except Exception:
            text = repr(chunk)
    text = "".join(ch if ch.isprintable() or ch in "\r\n\t" else "." for ch in text)
    return text.replace("\r", " ").replace("\n", " ")


def search_file(path: Path, needles: list[bytes], encodings: list[str],
                context: int, max_hits: int) -> list[str]:
    data = path.read_bytes()
    out: list[str] = []
    for i, (enc, needle) in enumerate(zip(encodings, needles)):
        start = 0
        hits = 0
        while hits < max_hits:
            off = data.find(needle, start)
            if off < 0:
                break
            ctx = decode_context(data, off, len(needle), "gbk" if i == 0 else "utf16", context)
            out.append(f"{path}|0x{off:08X}|{enc}|{ctx}")
            start = off + 1
            hits += 1
    return out


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("term")
    ap.add_argument("--dir", type=Path)
    ap.add_argument("--ext", default=",".join(DEFAULT_EXTS))
    ap.add_argument("--context", type=int, default=90)
    ap.add_argument("--max-hits", type=int, default=20)
    ap.add_argument("--out", type=Path)
    ap.add_argument("paths", nargs="*", type=Path)
    args = ap.parse_args(argv)

    exts = tuple(e.strip().lower() for e in args.ext.split(",") if e.strip())
    needles = [args.term.encode("gb18030"), args.term.encode("utf-16le")]
    encodings = ["gbk", "utf16le"]

    targets: list[Path] = list(args.paths)
    if args.dir:
        targets += [p for p in args.dir.rglob("*") if p.is_file() and p.suffix.lower() in exts]
    targets = [p for p in targets if p.is_file()]

    lines: list[str] = []
    for path in targets:
        try:
            lines += search_file(path, needles, encodings, args.context, args.max_hits)
        except (PermissionError, OSError):
            continue

    text = "\n".join(lines) + ("\n" if lines else "")
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8", errors="replace")
        print(f"{len(lines)} hits -> {args.out}")
    else:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
