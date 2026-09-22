"""Pretty-print one emitter's module tree from an embedded PSEF XML."""
from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def dump(el: ET.Element, indent: int, out: list[str], max_depth: int, depth: int = 0) -> None:
    if depth > max_depth:
        return
    attrs = " ".join(f'{k}="{v}"' for k, v in el.attrib.items() if k not in ("MetaType", "Type"))
    tag = el.tag
    mt = el.get("MetaType")
    ty = el.get("Type")
    label = tag
    if mt or ty:
        label += f"[{mt or ''}{'/' + ty if ty else ''}]"
    line = "  " * indent + f"<{label} {attrs}>"
    out.append(line.rstrip())
    kids = list(el)
    for k in kids[:200]:
        dump(k, indent + 1, out, max_depth, depth + 1)
    if len(kids) > 200:
        out.append("  " * (indent + 1) + f"... {len(kids) - 200} more")


def main() -> None:
    xml_path = Path(sys.argv[1])
    name = sys.argv[2]
    max_depth = int(sys.argv[3]) if len(sys.argv) > 3 else 8
    root = ET.parse(xml_path).getroot()
    for em in root.findall("Emitter"):
        if em.get("Name") == name:
            out: list[str] = [f"EMITTER name={name} attrs={dict(em.attrib)}"]
            for mod in em.findall("Module"):
                out.append("-" * 80)
                dump(mod, 0, out, max_depth)
            Path("_pss_emitter_dump.txt").write_text("\n".join(out), encoding="utf-8")
            print(f"wrote _pss_emitter_dump.txt lines={len(out)}")
            return
    print(f"emitter {name!r} not found")


if __name__ == "__main__":
    main()
