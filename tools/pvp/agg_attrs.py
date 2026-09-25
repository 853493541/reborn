#!/usr/bin/env python3
"""Aggregate ATTRIBUTE_TYPE usage per school from skill_cast_fields.tsv."""
from __future__ import annotations

import sys
from collections import Counter, defaultdict
from pathlib import Path

TSV = Path("proof/pvp/cast/skill_cast_fields.tsv")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    rows = [l.rstrip("\n").split("\t") for l in TSV.open(encoding="utf-8")][1:]
    per_school: dict[str, Counter] = defaultdict(Counter)
    for r in rows:
        school, attrs = r[1], r[6]
        if not attrs:
            continue
        for a in attrs.split(";"):
            per_school[school][a] += 1
    for school, cnt in sorted(per_school.items(), key=lambda x: -sum(x[1].values()))[:30]:
        print(f"== {school}")
        for k, v in cnt.most_common(14):
            print(f"   {v:5d} {k}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
