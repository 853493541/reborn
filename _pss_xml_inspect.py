"""Inspect the embedded PSEF XML structure."""
from __future__ import annotations

import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

TAG_RE = re.compile(r"<([A-Za-z_][\w:.-]*)")


def main() -> None:
    for raw in sys.argv[1:]:
        path = Path(raw)
        text = path.read_text(encoding="utf-8", errors="replace")
        print("=" * 100)
        print(f"FILE {path.name} chars={len(text)}")
        print("--- head 2000 ---")
        print(text[:2000])
        print("--- tag counts ---")
        c = Counter(TAG_RE.findall(text))
        for tag, n in c.most_common(40):
            print(f"  {tag}: {n}")
        try:
            root = ET.fromstring(text)
            print("--- root tree (2 levels) ---")
            for child in list(root)[:20]:
                print(
                    f"  {child.tag} attrs={dict(list(child.attrib.items())[:12])} children={len(child)}"
                )
        except ET.ParseError as exc:
            print("XML parse error:", exc)


if __name__ == "__main__":
    main()
