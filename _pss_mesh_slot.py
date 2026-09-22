"""Type-2 mesh-path slot analysis: offset, preceding bytes, and slot convention."""
from __future__ import annotations

import re
import struct
from pathlib import Path

from _pss_probe import parse_header

FILES = [
    Path("proof/compare/sfx_pakv4_flws_red/c_藏剑刀光01b红色.pss"),
    Path("proof/compare/sfx_pakv4_flws_red/c_藏剑风车范围_红色.pss"),
    Path("proof/compare/sfx_pakv4_flws/c_藏剑刀光01b.pss"),
    Path("proof/compare/sfx_pakv4_flws/c_藏剑风车范围.pss"),
]

MESH_RE = re.compile(rb"data[\\/][^\x00]{3,240}?\.(?:Mesh|mesh)")
NAME_RE = re.compile(rb"[\x20-\x7e\x81-\xfe]{2,60}")


def leading_name(blk: bytes) -> tuple[int, str]:
    end = blk.find(b"\x00")
    if end < 0:
        return 0, ""
    raw = blk[:end]
    for enc in ("gb18030", "latin1"):
        try:
            return end, raw.decode(enc)
        except UnicodeDecodeError:
            continue
    return end, repr(raw)


def main() -> None:
    for path in FILES:
        if not path.is_file():
            print(f"MISSING {path}")
            continue
        data = path.read_bytes()
        hdr = parse_header(data)
        print("=" * 100)
        print(f"{path.name} emitters={hdr['count']}")
        for i, e in enumerate(hdr["toc"]):
            if e["type"] != 2:
                continue
            blk = data[e["offset"] : e["offset"] + e["size"]]
            name_end, name = leading_name(blk)
            meshes = []
            for m in MESH_RE.finditer(blk):
                o = m.start()
                pre = blk[max(0, o - 8) : o]
                # u32 length candidate at o-4? path length?
                length_u32 = struct.unpack_from("<I", blk, o - 4)[0] if o >= 4 else None
                meshes.append(
                    {
                        "off": o,
                        "pre": pre.hex(),
                        "u32_before": length_u32,
                        "path_len": len(m.group(0)),
                        "path": m.group(0).decode("gb18030", errors="replace"),
                    }
                )
            print(f"  [{i:3d}] name_end={name_end} name={name!r} size={e['size']} meshes={meshes}")


if __name__ == "__main__":
    main()
