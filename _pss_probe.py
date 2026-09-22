"""Structural probe for raw PSS (PAR\\0) files — zero external references.

Dumps TOC, block census, alignment, strings and float candidates so the
format can be derived from bytes alone.
"""
from __future__ import annotations

import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
STAGED = [
    ROOT / "proof" / "compare" / "sfx_pakv4_flws_red",
    ROOT / "proof" / "compare" / "sfx_pakv4_flws",
]
GBK_RE = re.compile(rb"[\x81-\xfe][\x40-\xfe](?:[\x81-\xfe][\x40-\xfe]|[\x20-\x7e])+")


def gbk_strings(data: bytes, min_len: int = 4) -> list[str]:
    out: list[str] = []
    for m in GBK_RE.finditer(data):
        raw = m.group(0)
        if len(raw) < min_len:
            continue
        try:
            s = raw.decode("gb18030")
        except UnicodeDecodeError:
            continue
        if len(s.strip()) >= min_len:
            out.append(s)
    return out


def ascii_strings(data: bytes, min_len: int = 4) -> list[str]:
    out: list[str] = []
    cur = bytearray()
    for b in data:
        if 0x20 <= b < 0x7F:
            cur.append(b)
        else:
            if len(cur) >= min_len:
                out.append(cur.decode("latin1"))
            cur.clear()
    if len(cur) >= min_len:
        out.append(cur.decode("latin1"))
    return out


def parse_header(data: bytes) -> dict:
    if len(data) < 16:
        raise ValueError("too small")
    if data[:4] != b"PAR\x00":
        raise ValueError(f"bad magic {data[:4]!r}")
    version = struct.unpack_from("<H", data, 4)[0]
    unknown_u16 = struct.unpack_from("<H", data, 6)[0]
    unknown_u32 = struct.unpack_from("<I", data, 8)[0]
    count = struct.unpack_from("<I", data, 12)[0]
    toc = []
    for i in range(count):
        base = 16 + i * 12
        if base + 12 > len(data):
            raise ValueError(f"TOC truncated at {i}")
        t, off, size = struct.unpack_from("<III", data, base)
        toc.append({"i": i, "type": t, "offset": off, "size": size})
    return {
        "version": version,
        "unknown_u16": unknown_u16,
        "unknown_u32": unknown_u32,
        "count": count,
        "toc": toc,
        "toc_end": 16 + count * 12,
    }


def floats_in(data: bytes, start: int, end: int, limit: int = 48) -> list[tuple[int, float]]:
    out = []
    for off in range(start, min(end, start + limit * 4), 4):
        (v,) = struct.unpack_from("<f", data, off)
        out.append((off - start, v))
    return out


def main() -> None:
    files: list[Path] = []
    for folder in STAGED:
        if folder.is_dir():
            files.extend(sorted(folder.glob("*.pss")))
    if len(sys.argv) > 1:
        files = [Path(p) for p in sys.argv[1:]]

    for path in files:
        data = path.read_bytes()
        print("=" * 100)
        print(f"FILE {path.name}  bytes={len(data)}")
        try:
            hdr = parse_header(data)
        except ValueError as exc:
            print(f"  HEADER FAIL: {exc}")
            continue
        print(
            f"  version={hdr['version']} u16@6={hdr['unknown_u16']} u32@8={hdr['unknown_u32']} "
            f"emitters={hdr['count']} toc_end={hdr['toc_end']}"
        )
        toc = hdr["toc"]
        # TOC validity: offsets should be >= toc_end, sizes positive, ranges inside file,
        # and (ideally) sorted / contiguous.
        prev_end = hdr["toc_end"]
        for e in toc:
            ok = (
                e["offset"] >= hdr["toc_end"]
                and e["size"] > 0
                and e["offset"] + e["size"] <= len(data)
            )
            gap = e["offset"] - prev_end
            print(
                f"    [{e['i']:3d}] type={e['type']} off=0x{e['offset']:06x} size=0x{e['size']:06x} "
                f"({e['size']:6d}) gap_before={gap:6d} {'OK' if ok else 'BAD'}"
            )
            prev_end = e["offset"] + e["size"]
        print(f"  file_end - last_end = {len(data) - prev_end}")

        for e in toc:
            blk = data[e["offset"] : e["offset"] + e["size"]]
            print("-" * 100)
            print(f"  BLOCK type={e['type']} size={e['size']}")
            head = blk[: min(len(blk), 96)]
            for row in range(0, len(head), 16):
                chunk = head[row : row + 16]
                hexs = " ".join(f"{b:02x}" for b in chunk)
                asci = "".join(chr(b) if 0x20 <= b < 0x7F else "." for b in chunk)
                print(f"    {row:04x}  {hexs:<48}  {asci}")
            fl = floats_in(blk, 0, len(blk), limit=24)
            print("    floats@start:", " ".join(f"{o}:{v:.4g}" for o, v in fl))
            ss = gbk_strings(blk)
            if ss:
                print("    gbk strings:", ss[:12])
            asc = [s for s in ascii_strings(blk) if len(s) >= 5]
            if asc:
                print("    ascii strings:", asc[:12])
        print()


if __name__ == "__main__":
    main()
