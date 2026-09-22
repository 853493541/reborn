"""Enumerate the PACK zip tail of a PSS file."""
from __future__ import annotations

import io
import struct
import sys
import zipfile
from pathlib import Path

from _pss_probe import parse_header


def main() -> None:
    for raw in sys.argv[1:]:
        path = Path(raw)
        data = path.read_bytes()
        hdr = parse_header(data)
        last = max(e["offset"] + e["size"] for e in hdr["toc"])
        magic = data[last : last + 4]
        (declared,) = struct.unpack_from("<I", data, last + 4)
        pk = last + 8
        print("=" * 100)
        print(f"{path.name}: tail_magic={magic!r} declared={declared} pk_at=0x{pk:x} file={len(data)}")
        blob = data[pk:]
        try:
            zf = zipfile.ZipFile(io.BytesIO(blob))
        except Exception as exc:
            print("  zip open failed:", exc)
            continue
        for info in zf.infolist():
            print(
                f"  {info.filename!r} method={info.compress_type} csize={info.compress_size} "
                f"usize={info.file_size} crc=0x{info.CRC:08x}"
            )
        # dump first bytes of each entry
        for info in zf.infolist():
            entry = zf.read(info)
            head = entry[:32]
            print(f"    head[{info.filename!r}]: {head.hex(' ')} | {head[:16]!r}")


if __name__ == "__main__":
    main()
