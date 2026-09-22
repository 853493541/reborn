"""Extract the embedded PSEF XML from a raw PSS PACK tail (original bytes only)."""
from __future__ import annotations

import io
import struct
import sys
import zipfile
from pathlib import Path

from _pss_probe import parse_header


def extract(path: Path, out: Path | None = None) -> Path:
    data = path.read_bytes()
    hdr = parse_header(data)
    last = max(e["offset"] + e["size"] for e in hdr["toc"])
    assert data[last : last + 4] == b"PACK"
    (size,) = struct.unpack_from("<I", data, last + 4)
    blob = data[last + 8 : last + 8 + size]
    with zipfile.ZipFile(io.BytesIO(blob)) as zf:
        info = zf.infolist()[0]
        payload = zf.read(info)
    if out is None:
        out = Path("_pss_xml") / (path.stem + ".xml")
    out.parent.mkdir(exist_ok=True)
    out.write_bytes(payload)
    return out


if __name__ == "__main__":
    for raw in sys.argv[1:]:
        p = extract(Path(raw))
        print(f"wrote {p} ({p.stat().st_size} bytes)")
