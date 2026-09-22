"""Focused block dump for one PSS block: hex + u32 + float + GBK string offsets."""
from __future__ import annotations

import struct
import sys
from pathlib import Path

from _pss_probe import gbk_strings, parse_header


def strings_with_offsets(blk: bytes) -> list[tuple[int, str]]:
    out: list[tuple[int, str]] = []
    i = 0
    n = len(blk)
    while i < n:
        b = blk[i]
        if (0x20 <= b < 0x7F) or (0x81 <= b <= 0xFE and i + 1 < n and blk[i + 1] >= 0x40):
            end = i
            while end < n:
                c = blk[end]
                if 0x20 <= c < 0x7F:
                    end += 1
                elif 0x81 <= c <= 0xFE and end + 1 < n and blk[end + 1] >= 0x40:
                    end += 2
                else:
                    break
            raw = blk[i:end]
            try:
                s = raw.decode("gb18030")
            except UnicodeDecodeError:
                s = raw.decode("latin1")
            if len(s) >= 4:
                out.append((i, s))
            i = end
        else:
            i += 1
    return out


def dump(path: Path, type_wanted: int, index: int, max_bytes: int = 2048) -> None:
    data = path.read_bytes()
    hdr = parse_header(data)
    blocks = [e for e in hdr["toc"] if e["type"] == type_wanted]
    if index >= len(blocks):
        print(f"no type={type_wanted} index={index} (have {len(blocks)})")
        return
    e = blocks[index]
    blk = data[e["offset"] : e["offset"] + e["size"]]
    print(f"FILE {path.name}")
    print(f"BLOCK type={type_wanted} nth={index} size={e['size']} off=0x{e['offset']:x}")
    show = min(len(blk), max_bytes)
    for row in range(0, show, 16):
        chunk = blk[row : row + 16]
        hexs = " ".join(f"{b:02x}" for b in chunk)
        asci = "".join(chr(b) if 0x20 <= b < 0x7F else "." for b in chunk)
        print(f"{row:04x}  {hexs:<48}  {asci}")
    print("--- u32 @ every 4 bytes (non-zero only) ---")
    for o in range(0, show - 3, 4):
        (u,) = struct.unpack_from("<I", blk, o)
        if not u:
            continue
        tag = ""
        if 0x20 <= blk[o] < 0x7F:
            tag = "  <- ascii-ish"
        print(f"  +{o:04x} u32={u:10d} (0x{u:08x}){tag}")
    print("--- floats @ every 4 bytes (plausible -1e6..1e6) ---")
    for o in range(0, show - 3, 4):
        (f,) = struct.unpack_from("<f", blk, o)
        if f == f and abs(f) > 1e-6 and abs(f) < 1e6 and f not in (0.0,):
            print(f"  +{o:04x} f32={f:.6g}")
    print("--- strings ---")
    for o, s in strings_with_offsets(blk[:show]):
        print(f"  +{o:04x} {s!r}")


def tail(path: Path, start: int, length: int = 512) -> None:
    data = path.read_bytes()
    hdr = parse_header(data)
    last = max(e["offset"] + e["size"] for e in hdr["toc"])
    print(f"FILE {path.name} TOC last_end=0x{last:x} file_len=0x{len(data):x} tail={len(data)-last}")
    blk = data[start : start + length]
    for row in range(0, len(blk), 16):
        chunk = blk[row : row + 16]
        hexs = " ".join(f"{b:02x}" for b in chunk)
        asci = "".join(chr(b) if 0x20 <= b < 0x7F else "." for b in chunk)
        print(f"{start+row:06x}  {hexs:<48}  {asci}")
    print("--- strings in tail ---")
    for o, s in strings_with_offsets(data[last:]):
        print(f"  +{last+o:06x} {s!r}")


if __name__ == "__main__":
    argv = [a for a in sys.argv[1:] if a != "--full"]
    full = "--full" in sys.argv
    action = argv[0]
    path = Path(argv[1])
    if action == "tail":
        tail(path, int(argv[2], 0), int(argv[3]) if len(argv) > 3 else 512)
    else:
        dump(path, int(argv[2]), int(argv[3]), 10**9 if full else (int(argv[4]) if len(argv) > 4 else 2048))
