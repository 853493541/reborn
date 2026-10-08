#!/usr/bin/env python3
"""Compare a renderer heightmap (.r32) with its physics counterpart (.bch).

Both are float32 grids for the same region:
  .r32  landscape/heightmap/<map>_i_j.r32   - 513^2 floats, no header
  .bch  landscape/heightmap_bc/<map>_i_j.bch - 36-byte header + samples^2 floats
The BCH is row-flipped in Z relative to the R32 and affine-related to it
(correlation 1.0, residual 0 at float precision for 龙门寻宝_002_002; see
docs/movement/TERRAIN_R32_BCH_RELATION.md, 2026-10-04).

Usage:
  .venv\\Scripts\\python.exe tools\\movement\\r32_bch_relation.py <a.r32> <b.bch>
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path


def stats(vals):
    mn, mx = min(vals), max(vals)
    mean = sum(vals) / len(vals)
    var = sum((v - mean) ** 2 for v in vals) / len(vals)
    return mn, mx, mean, var ** 0.5


def main(argv):
    if len(argv) != 3:
        print(__doc__)
        return 2
    r32_path, bch_path = Path(argv[1]), Path(argv[2])
    r32 = struct.unpack("<%df" % (r32_path.stat().st_size // 4), r32_path.read_bytes())
    b = bch_path.read_bytes()
    magic = b[0:4].hex(" ")
    samples, cells = struct.unpack_from("<II", b, 4)
    hdr_size = struct.unpack_from("<I", b, 20)[0]
    f28, f32 = struct.unpack_from("<ff", b, 28)
    n = samples
    bch = struct.unpack_from("<%df" % (n * n), b, hdr_size)
    print("r32: %d floats  min=%.6f max=%.6f mean=%.6f std=%.6f"
          % ((len(r32),) + stats(r32)))
    print("bch: magic=%s samples=%d cells=%d hdr=%d floats=%d min=%.6f max=%.6f mean=%.6f std=%.6f"
          % ((magic, samples, cells, hdr_size, len(bch)) + stats(bch)))
    print("bch header floats @28=%.2f @32=%.2f" % (f28, f32))
    if len(r32) != n * n:
        print("grid mismatch: r32 %d != bch %d - not comparable" % (len(r32), n * n))
        return 1
    flip = [0.0] * (n * n)
    for r in range(n):
        flip[(n - 1 - r) * n:(n - r) * n] = bch[r * n:(r + 1) * n]
    mr = sum(r32) / len(r32)
    mf = sum(flip) / len(flip)
    cov = sum((x - mr) * (y - mf) for x, y in zip(r32, flip)) / len(r32)
    vf = sum((y - mf) ** 2 for y in flip) / len(flip)
    slope = cov / vf
    inter = mr - slope * mf
    resid = max(abs(x - (slope * y + inter)) for x, y in zip(r32, flip))
    print("affine r32 = %.6f * bch_flip + %.6f   max residual = %.9f" % (slope, inter, resid))
    print("same-order equality: %d / %d" % (sum(1 for x, y in zip(r32, bch) if x == y), len(r32)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
