"""Analyze .foliage instance layout (corrected header parsing).

Header: magic 'FOLI', u32 version, u32 size, u32 patternCount, u32 instanceCount,
u32 lodCount, then pattern records (72 bytes: 38-byte GUID + 33 bytes data),
then instance data.
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path


def main() -> None:
    b = Path(sys.argv[1]).read_bytes()
    magic, version, size, pat_count, inst_count, lod = struct.unpack_from("<4sIIIII", b, 0)
    print(f"size={len(b)} magic={magic!r} ver={version} sizeField={size} patterns={pat_count} instances={inst_count} lod={lod}")

    off = 40
    for i in range(pat_count):
        rec = b[off : off + 72]
        guid = rec[:38].decode("ascii", "replace")
        tail = rec[38:]
        print(f"  pattern[{i}] @{off} {guid}")
        # try interpret tail as: u8 pad? then floats
        for skip in (1, 2, 4):
            fl = struct.unpack_from("<%df" % ((len(tail) - skip) // 4), tail, skip)
            print(f"     skip{skip}:", " ".join(f"{x:.3f}" for x in fl[:10]))
        off += 72
    start = off
    print("instances start at", start, "bytes left", len(b) - start)
    print("bytes/instance =", (len(b) - start) / max(inst_count, 1))

    # try candidate strides with a plausibility score over many records
    for s in range(8, 65, 4):
        n = (len(b) - start) // s
        if n < 10:
            continue
        good = 0
        for i in range(min(n, 400)):
            p = start + i * s
            vals = struct.unpack_from("<%df" % (s // 4), b[p : p + s])
            # position plausibility: at least two values in world range
            inrange = sum(1 for v in vals if -500000 < v < 500000)
            finite = all(abs(v) < 1e9 for v in vals)
            if finite and inrange == len(vals):
                good += 1
        if good > 300:
            print(f"  stride {s}: plausible {good}/{n}")


if __name__ == "__main__":
    main()
