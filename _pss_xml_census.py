"""Structural census of the PSEF DataStorage XML embedded in raw PSS files."""
from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path


def census(path: Path) -> None:
    root = ET.parse(path).getroot()
    print("=" * 100)
    print(f"XML {path.name}")
    print("  root:", root.tag, dict(root.attrib))
    emitters = list(root)
    print(f"  emitters: {len(emitters)}")
    mod_types: Counter = Counter()
    attrs: Counter = Counter()
    for em in emitters:
        for k in em.attrib:
            attrs[k] += 1
        for mod in em:
            mt = mod.attrib.get("ModuleType", mod.tag)
            mod_types[mt] += 1
    print("  emitter attrs (count):")
    for k, n in attrs.most_common():
        print(f"    {k}: {n}")
    print("  module types (count):")
    for k, n in mod_types.most_common():
        print(f"    {k}: {n}")
    # resource refs anywhere
    refs: Counter = Counter()
    for el in root.iter():
        for k, v in el.attrib.items():
            low = str(v).lower()
            if any(ext in low for ext in (".tga", ".dds", ".mesh", ".ani", ".jsondef", ".jsoninspack", ".sfx")):
                refs[(k, v)] += 1
    print("  resource refs:")
    for (k, v), n in refs.most_common(60):
        print(f"    {k}={v!r} x{n}")


if __name__ == "__main__":
    for p in sys.argv[1:]:
        census(Path(p))
