"""Probe GATA .tani for timing-like records (ints/floats) — native only."""
from __future__ import annotations

import struct
import sys
from pathlib import Path


def main() -> None:
    for raw in sys.argv[1:]:
        path = Path(raw)
        data = path.read_bytes()
        print("=" * 100)
        print(f"FILE {path.name} bytes={len(data)}")
        # header
        magic = data[:4].decode("latin1")
        version = struct.unpack_from("<I", data, 4)[0]
        print(f"  magic={magic!r} version={version}")
        # u16 values 1..20000 with offsets
        hits = []
        for i in range(8, len(data) - 1):
            (v,) = struct.unpack_from("<H", data, i)
            if 1 <= v <= 20000 and v not in (0x0100, 0x0001):
                hits.append((i, v))
        # compress: show first 60
        print(f"  u16 candidates: {len(hits)}")
        for off, v in hits[:60]:
            print(f"    @0x{off:04x} u16={v}")
        # u32 values 100..100000
        hits32 = []
        for i in range(8, len(data) - 3):
            (v,) = struct.unpack_from("<I", data, i)
            if 500 <= v <= 100000:
                hits32.append((i, v))
        print(f"  u32 candidates: {len(hits32)}")
        for off, v in hits32[:60]:
            print(f"    @0x{off:04x} u32={v}")


if __name__ == "__main__":
    main()
