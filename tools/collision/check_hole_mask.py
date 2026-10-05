#!/usr/bin/env python3
"""Compare an engine hole-mask dump with an extracted .hlb file.

The decoded conversion (PhysicsEngineX64 `_ConvertHoleData` @ RVA 0x32e90,
proof/collision/disasm/hole_convert_holedata.txt): the .hlb is
(RegionSize+1)^2 raw bytes, row = Z, col = X, one byte per height sample
(255 = solid, 0 = hole). A cell (x, z) is a hole only when all four corner
samples are <= 0x7F. The packed mask has one bit per cell: byte
z * ceil(n/8) + (x >> 3), bit (x & 7), set = hole.

The engine's packed mask is **row-flipped in Z** relative to the raw file
(engine cell z = n - 1 - z_raw). Verified by A/B on 海岛绝境_000_000 against
a live client dump: 234/234 hole cells match with the flip, 0/234 without.
This tool applies the flip, so --dump comparisons are engine-exact.

Usage:
  python tools/collision/check_hole_mask.py --hlb <file.hlb> --dump <holes_*.bin>
  python tools/collision/check_hole_mask.py --hlb <file.hlb> --spawn-cells 8
  python tools/collision/check_hole_mask.py --scan <dir>   # inventory a .hlb tree

Dump format (written by reborn_client with RC_HOLE_DUMP=<dir>):
  u32 magic=1, u32 n, i32 regionX, i32 regionZ, u32 byteLen, bytes[len*?].

Exit code 0 when the masks match (or when only --spawn-cells is used).
"""
import argparse
import struct
import sys


def convert_hlb(raw: bytes, n: int) -> bytes:
    if len(raw) != (n + 1) * (n + 1):
        raise ValueError("hlb size %d != (n+1)^2=%d" % (len(raw), (n + 1) * (n + 1)))
    stride = n + 1
    row_bytes = (n + 7) // 8
    mask = bytearray(row_bytes * n)
    holes = 0
    for z in range(n):
        base = z * stride
        out = (n - 1 - z) * row_bytes
        for x in range(n):
            i = base + x
            if (raw[i] <= 0x7F and raw[i + 1] <= 0x7F
                    and raw[i + stride] <= 0x7F and raw[i + stride + 1] <= 0x7F):
                mask[out + (x >> 3)] |= 1 << (x & 7)
                holes += 1
    return bytes(mask), holes


def read_dump(path: str):
    with open(path, "rb") as f:
        data = f.read()
    magic, n, rx, rz, ln = struct.unpack_from("<IIiiI", data, 0)
    if magic != 1:
        raise ValueError("bad dump magic %d" % magic)
    mask = data[20:20 + ln]
    if len(mask) != ln:
        raise ValueError("truncated dump")
    return n, rx, rz, mask


def hole_cells(mask_hlb: bytes, n: int):
    row_bytes = (n + 7) // 8
    for z in range(n):
        for x in range(n):
            if mask_hlb[z * row_bytes + (x >> 3)] & (1 << (x & 7)):
                yield x, z


def scan_tree(directory: str) -> int:
    """Inventory every .hlb under ``directory`` (per-file hole-cell count)."""
    import pathlib
    root = pathlib.Path(directory)
    files = sorted(root.rglob("*.hlb"))
    total = 0
    for f in files:
        raw = f.read_bytes()
        n = int(round(len(raw) ** 0.5)) - 1
        if n <= 0 or (n + 1) * (n + 1) != len(raw):
            print("%-90s size=%d (not (n+1)^2)" % (f, len(raw)))
            continue
        _, holes = convert_hlb(raw, n)
        total += holes
        print("%-90s n=%d holeCells=%d" % (f, n, holes))
    print("scan: %d files, %d hole cells total" % (len(files), total))
    return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--hlb")
    ap.add_argument("--dump")
    ap.add_argument("--spawn-cells", type=int, default=0)
    ap.add_argument("--origin", default="-102400,-102400")
    ap.add_argument("--cell", type=float, default=100.0)
    ap.add_argument("--scan")
    args = ap.parse_args()

    if args.scan:
        return scan_tree(args.scan)
    if not args.hlb:
        ap.error("--hlb is required unless --scan is used")

    raw = open(args.hlb, "rb").read()
    n = int(round(len(raw) ** 0.5)) - 1
    if n <= 0 or (n + 1) * (n + 1) != len(raw):
        print("FAIL: bad hlb size %d" % len(raw))
        return 2
    mask, holes = convert_hlb(raw, n)
    print("hlb: n=%d cells=%d holeCells=%d" % (n, n * n, holes))

    if args.spawn_cells > 0:
        ox, oz = [float(v) for v in args.origin.split(",")]
        shown = 0
        for cx, cz in hole_cells(mask, n):
            # converted/engine mask row cz -> world cell z = n-1-cz (the same
            # flip SampleGround applies; verified by the 2026-09-29 hole fall:
            # world (22850,30450) on 海岛绝境 is a hole only under this mapping)
            wx = ox + (cx + 0.5) * args.cell
            wz = oz + (n - 1 - cz + 0.5) * args.cell
            print("hole cell (%d,%d) -> world (%.0f,%.0f)" % (cx, cz, wx, wz))
            shown += 1
            if shown >= args.spawn_cells:
                break

    if args.dump:
        dn, rx, rz, dmask = read_dump(args.dump)
        if dn != n:
            print("FAIL: dump n=%d hlb n=%d" % (dn, n))
            return 2
        if dmask == mask:
            print("PASS: dump region (%d,%d) mask identical (%d bytes)" % (rx, rz, len(mask)))
            return 0
        diff = sum(1 for a, b in zip(dmask, mask) if a != b)
        first = next((i for i, (a, b) in enumerate(zip(dmask, mask)) if a != b), -1)
        print("FAIL: dump region (%d,%d) differs in %d/%d bytes (first at %d)"
              % (rx, rz, diff, len(mask), first))
        for i in [first]:
            if i >= 0:
                rb = (n + 7) // 8
                print("  byte %d -> cell row z=%d x=%d..%d" % (i, i // rb, (i % rb) * 8, (i % rb) * 8 + 7))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
