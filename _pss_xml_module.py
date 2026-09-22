"""Pretty-print selected modules of one emitter from the embedded PSEF XML."""
from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def indent(el: ET.Element, level: int = 0) -> str:
    pad = "  " * level
    attrs = " ".join(f'{k}="{v}"' for k, v in el.attrib.items())
    line = f"{pad}<{el.tag}{(' ' + attrs) if attrs else ''}>"
    kids = list(el)
    if not kids and (el.text or "").strip() == "":
        return line
    out = [line]
    for kid in kids:
        out.append(indent(kid, level + 1))
    return "\n".join(out)


def main() -> None:
    path = Path(sys.argv[1])
    emitter_index = int(sys.argv[2])
    wanted = set(sys.argv[3:])
    root = ET.parse(path).getroot()
    emitters = root.findall("Emitter")
    em = emitters[emitter_index]
    print(f"EMITTER[{emitter_index}] {em.attrib.get('Name')!r}")
    print("  attrs:", dict(em.attrib))
    for mod in em.findall("Module"):
        mt = mod.attrib.get("ModuleType", "?")
        if wanted and mt not in wanted:
            continue
        print("-" * 90)
        print(indent(mod))


if __name__ == "__main__":
    main()
