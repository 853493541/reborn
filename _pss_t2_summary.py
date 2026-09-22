"""Summarize type-2 inventory; scan every block for .Mesh refs robustly."""
import json
import re
from pathlib import Path

from _pss_probe import parse_header

PSS = [
    Path("proof/compare/sfx_pakv4_flws_red/c_藏剑刀光01b红色.pss"),
    Path("proof/compare/sfx_pakv4_flws_red/c_藏剑风车范围_红色.pss"),
]


def decode_path(raw: bytes) -> str:
    for enc in ("gb18030", "latin1"):
        try:
            return raw.decode(enc)
        except UnicodeDecodeError:
            continue
    return repr(raw)


def mesh_refs(blk: bytes) -> list[str]:
    out = []
    for m in re.finditer(rb"[\x20-\x7e\x81-\xfe]{3,240}?\.(?:Mesh|mesh)", blk):
        raw = m.group(0)
        s = decode_path(raw)
        if "data" in s.lower() or "/" in s or "\\" in s:
            out.append(s)
    return out


d = json.loads(Path("_pss_t2_inventory.json").read_text(encoding="utf-8"))
lines = []
for path in PSS:
    data = path.read_bytes()
    hdr = parse_header(data)
    lines.append("=" * 90)
    lines.append(f"FILE {path.name}")
    inv = {r["index"]: r for r in d.get(path.name, [])}
    for i, e in enumerate(hdr["toc"]):
        if e["type"] != 2:
            continue
        blk = data[e["offset"] : e["offset"] + e["size"]]
        name = inv.get(i, {}).get("name", "")
        refs = mesh_refs(blk)
        lines.append(f"  [{i:3d}] size={e['size']:5d} name={name!r} mesh_refs={refs}")
Path("_pss_t2_summary.txt").write_text("\n".join(lines), encoding="utf-8")
print("\n".join(lines))
