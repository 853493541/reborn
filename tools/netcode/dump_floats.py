#!/usr/bin/env python3
"""Read float/double constants at given VAs from a PE (defaults embedded in loaders).

Usage: python tools/netcode/dump_floats.py BINARY va1 [va2 ...]
       (or: --list file with 'label \t 0xVA' lines)
"""
from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

import pefile


def read_va(pe, va: int, size: int) -> bytes | None:
    base = pe.OPTIONAL_HEADER.ImageBase
    rva = va - base
    try:
        return pe.get_data(rva, size)
    except Exception:
        return None


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("binary", type=Path)
    ap.add_argument("vas", nargs="*")
    ap.add_argument("--list", type=Path, help="file of 'label<TAB>0xVA' lines")
    args = ap.parse_args(argv)

    entries: list[tuple[str, int]] = []
    if args.list:
        for line in args.list.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            parts = line.split()
            if len(parts) >= 2:
                entries.append((" ".join(parts[:-1]), int(parts[-1], 0)))
    for va in args.vas:
        entries.append((hex(int(va, 0)), int(va, 0)))

    pe = pefile.PE(str(args.binary), fast_load=False)
    for label, va in entries:
        data = read_va(pe, va, 8)
        if not data or len(data) < 4:
            print(f"{label}\t{va:#x}\t(unmapped)")
            continue
        f32 = struct.unpack_from("<f", data)[0]
        f64 = struct.unpack_from("<d", data)[0]
        print(f"{label}\t{va:#x}\tf32={f32!r}\tf64={f64!r}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
