"""Extract and inspect the PACK/PSEF XML data store inside a raw PSS."""
from __future__ import annotations

import io
import struct
import sys
import zipfile
from pathlib import Path

from _pss_probe import parse_header


def extract_xml(path: Path) -> tuple[bytes, str]:
    data = path.read_bytes()
    hdr = parse_header(data)
    last = max(e["offset"] + e["size"] for e in hdr["toc"])
    assert data[last : last + 4] == b"PACK", "no PACK"
    (size,) = struct.unpack_from("<I", data, last + 4)
    blob = data[last + 8 : last + 8 + size]
    with zipfile.ZipFile(io.BytesIO(blob)) as zf:
        name = zf.infolist()[0].filename
        return zf.read(name), name


def main() -> None:
    for p in sys.argv[1:]:
        path = Path(p)
        xml, name = extract_xml(path)
        out = Path("_tmp_pss_export") / (path.stem + "." + name + ".xml")
        out.parent.mkdir(exist_ok=True)
        out.write_bytes(xml)
        print(f"{path.name}: entry={name!r} bytes={len(xml)} -> {out}")
        text = xml.decode("utf-8", errors="replace")
        lines = text.splitlines()
        print(f"  lines={len(lines)} head:")
        for line in lines[:12]:
            print("   ", line[:180])


if __name__ == "__main__":
    main()
