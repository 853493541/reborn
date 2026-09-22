"""Per-emitter resource binding report from the embedded PSEF XML."""
from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

STRING_TAGS = {"PEVariant"}


def variant_strings(module: ET.Element) -> list[str]:
    out = []
    for el in module.iter():
        if el.tag == "PEVariant" and el.get("Type", "").lstrip("/") == "String":
            v = el.get("Value", "")
            if v:
                out.append(v)
    return out


def active_type(module: ET.Element) -> str:
    for el in module.iter("EMData"):
        at = el.get("ActiveType")
        if at:
            return at
    return ""


PATHISH = (".tga", ".dds", ".mesh", ".Mesh", ".def", ".jsondef", ".ani", ".sfx")


def main() -> None:
    for raw in sys.argv[1:]:
        path = Path(raw)
        root = ET.parse(path).getroot()
        print("=" * 100)
        print(f"FILE {path.name}")
        for i, em in enumerate(root.findall("Emitter")):
            attrs = em.attrib
            print(f"  [{i:2d}] {attrs.get('Name')!r} id={attrs.get('ID')} "
                  f"dur={attrs.get('DurationTime')} delay={attrs.get('DelayTime')} "
                  f"face={attrs.get('FaceType')} motion={attrs.get('MotionType')} "
                  f"blend={attrs.get('BlendType')}")
            for mod in em.findall("Module"):
                mt = mod.get("ModuleType", "?")
                if mt == "Particle Type":
                    print(f"        Particle Type active={active_type(mod)!r}")
                strings = variant_strings(mod)
                if not strings:
                    continue
                paths = [s for s in strings if s.lower().endswith(PATHISH)]
                labels = [s for s in strings if not s.lower().endswith(PATHISH)]
                if paths or labels:
                    print(f"        {mt}:")
                    if paths:
                        for p in paths:
                            print(f"            path: {p}")
                    if labels:
                        print(f"            labels: {labels}")


if __name__ == "__main__":
    main()
