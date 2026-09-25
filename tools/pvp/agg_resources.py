#!/usr/bin/env python3
"""Aggregate resource-related identifiers in skill scripts per school."""
from __future__ import annotations

import os
import re
import sys
from collections import Counter, defaultdict

ROOT = (
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4"
    r"\jx3-web-map-viewer\cache-extraction\pakv4-probe"
    r"\ability-matcher\extracted\scripts\skill"
)
PAT = re.compile(
    r"\b(n?[A-Za-z_]*?(?:Rage|Mana|Energy|SunEnergy|MoonEnergy|Accumulate|QiEnergy|Stamina)[A-Za-z_]*)\b"
)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    per_school: dict[str, Counter] = defaultdict(Counter)
    ex: dict[str, str] = {}
    for dirpath, _dirs, files in os.walk(ROOT):
        for fn in files:
            if not fn.lower().endswith(".lua"):
                continue
            p = os.path.join(dirpath, fn)
            rel = os.path.relpath(p, ROOT)
            school = rel.split(os.sep)[0]
            b = open(p, "rb").read()
            if b[:4] == b"\x1bLua":
                continue
            t = b.decode("gb18030", errors="replace")
            for m in PAT.finditer(t):
                k = m.group(1)
                per_school[school][k] += 1
                ex.setdefault(k, rel)
    for school, cnt in sorted(per_school.items(), key=lambda x: -sum(x[1].values()))[:24]:
        print(f"== {school}")
        for k, v in cnt.most_common(10):
            print(f"   {v:5d} {k:<34} e.g. {ex[k]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
