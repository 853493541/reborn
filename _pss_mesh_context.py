"""Locate where mesh paths live in the PSEF XML (module + emitter context)."""
from __future__ import annotations

import sys
from pathlib import Path
from xml.etree import ElementTree as ET


def main() -> None:
    for raw in sys.argv[1:]:
        path = Path(raw)
        tree = ET.parse(path)
        root = tree.getroot()
        print("=" * 90)
        print(f"FILE {path.name}")
        for em in root.findall("Emitter"):
            for mod in em.findall("Module"):
                for el in mod.iter("PEVariant"):
                    value = el.get("Value", "")
                    if value.lower().endswith((".mesh", ".ani")):
                        print(
                            f"  emitter={em.get('Name')!r} module={mod.get('ModuleType')!r} "
                            f"type_attr={el.get('Type')!r} value={value!r}"
                        )


if __name__ == "__main__":
    main()
