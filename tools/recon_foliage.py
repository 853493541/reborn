"""Reverse the JX3 .foliage instance format (magic FOLI).

Prints header fields, locates pattern UUID strings, infers the record stride,
and decodes candidate transform floats for the first records.

Usage: python tools/recon_foliage.py <file.foliage> [count]
"""
from __future__ import annotations

import re
import struct
import sys
from pathlib import Path

GUID_RE = re.compile(rb"\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}")


def main() -> None:
    path = Path(sys.argv[1])
    show = int(sys.argv[2]) if len(sys.argv) > 2 else 6
    b = path.read_bytes()
    print(f"file {path.name} size={len(b)}")

    magic, version, size_field, f1, f2, f3 = struct.unpack_from("<4sIIIII", b, 0)
    print(f"  magic={magic!r} version={version} sizeField={size_field} f1={f1} f2={f2} f3={f3}")

    guids = [m.start() for m in GUID_RE.finditer(b)]
    print(f"  GUID strings: {len(guids)}")
    if not guids:
        return
    deltas = [guids[i + 1] - guids[i] for i in range(min(len(guids) - 1, 30))]
    print(f"  first GUID offsets: {guids[:6]}")
    print(f"  deltas: {deltas[:12]}")
    stride_guess = max(set(deltas), key=deltas.count)
    print(f"  most common delta (stride guess): {stride_guess}")

    # header size = first GUID offset - assumed GUID at record start
    start = guids[0]
    print(f"  first record start guess: {start}")
    rec = stride_guess
    n = (len(b) - start) // rec if rec else 0
    print(f"  records by stride: {n}")

    for i in range(min(show, n)):
        off = start + i * rec
        raw = b[off : off + rec]
        guid = GUID_RE.match(raw)
        g = guid.group().decode() if guid else "?"
        tail = raw[len(g) + 1 :] if guid else raw
        floats = struct.unpack_from(f"<{len(tail)//4}f", tail)
        print(f"  rec{i} @{off} guid={g}")
        print("    floats:", " ".join(f"{x:.2f}" for x in floats[:16]))


if __name__ == "__main__":
    main()
