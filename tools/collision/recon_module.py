#!/usr/bin/env python3
"""Dump PE exports and keyword-filtered ASCII strings from a module.

Usage: python tools/collision/recon_module.py <dll> <out-prefix> [keyword ...]

Produces <out-prefix>_exports.txt and <out-prefix>_strings.txt (offset, string).
"""
import re
import sys
from pathlib import Path

import pefile

DEFAULT_KEYS = [
    "nav", "path", "mesh", "tile", "obstacle", "query", "capsule", "solver",
    "collision", "ray", "trigger", "missile", "bullet", "water", "swim",
]

ASCII_RUN = re.compile(rb"[\x20-\x7e]{5,}")
UTF16_RUN = re.compile(rb"(?:[\x20-\x7e]\x00){4,}")


def dump_exports(pe, out):
    lines = []
    if hasattr(pe, "DIRECTORY_ENTRY_EXPORT"):
        for exp in pe.DIRECTORY_ENTRY_EXPORT.symbols:
            name = exp.name.decode("ascii", "replace") if exp.name else ""
            lines.append("%08x ord=%-5d %s" % (exp.address, exp.ordinal, name))
    out.write_text("\n".join(sorted(lines)) + "\n", encoding="utf-8")
    return len(lines)


def dump_strings(data, keys, out):
    hits = []
    for m in ASCII_RUN.finditer(data):
        s = m.group().decode("ascii", "replace")
        low = s.lower()
        if any(k in low for k in keys):
            hits.append("0x%08x ascii  %s" % (m.start(), s))
    for m in UTF16_RUN.finditer(data):
        s = m.group().decode("utf-16-le", "replace")
        low = s.lower()
        if any(k in low for k in keys):
            hits.append("0x%08x utf16  %s" % (m.start(), s))
    out.write_text("\n".join(hits) + "\n", encoding="utf-8")
    return len(hits)


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    dll = Path(sys.argv[1])
    prefix = Path(sys.argv[2])
    keys = [k.lower() for k in (sys.argv[3:] or DEFAULT_KEYS)]
    pe = pefile.PE(str(dll), fast_load=True)
    pe.parse_data_directories(
        directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXPORT"]]
    )
    n_exp = dump_exports(pe, Path(str(prefix) + "_exports.txt"))
    n_str = dump_strings(dll.read_bytes(), keys, Path(str(prefix) + "_strings.txt"))
    print("%s: %d exports, %d string hits" % (dll.name, n_exp, n_str))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
