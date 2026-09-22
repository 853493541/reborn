"""Structural inventory of the PSEF XML embedded in a PSS."""
from __future__ import annotations

import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path


def walk_modules(emitter: ET.Element) -> list[str]:
    return [m.get("ModuleType", "?") for m in emitter.findall("Module")]


def main() -> None:
    for raw in sys.argv[1:]:
        path = Path(raw)
        text = path.read_text(encoding="utf-8", errors="replace")
        root = ET.fromstring(text)
        print("=" * 100)
        print(f"FILE {path.name} root={root.tag}")
        print("root attrs:", dict(list(root.attrib.items())[:20]))
        emitters = root.findall("Emitter")
        print(f"emitters={len(emitters)}")
        for em in emitters:
            attrs = dict(em.attrib)
            keys = ["Name", "ID", "DurationTime", "DelayTime", "MotionType", "FaceType", "BlendType", "ParticleCreateMode", "ParticleEvent"]
            shown = {k: attrs.get(k) for k in keys if k in attrs}
            mods = walk_modules(em)
            print(f"  - {shown}")
            print(f"      modules({len(mods)}): {mods}")
        # resource refs anywhere
        refs = sorted(set(re.findall(r'[A-Za-z0-9_\\/. :\u4e00-\u9fff-]+\.(?:tga|dds|Mesh|mesh|ani|jsondef)', text, re.I)))
        print(f"resource-like refs ({len(refs)}):")
        for r in refs[:60]:
            print("   ", r)
        # value types
        types = Counter(el.get("Type") for el in root.iter() if el.tag in ("Value", "EMData", "Element", "KeyPoint"))
        print("value/element types:", dict(types.most_common(20)))


if __name__ == "__main__":
    main()
