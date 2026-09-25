#!/usr/bin/env python3
"""List active (comment-stripped) resource fields with values per school."""
from __future__ import annotations

import json
import sys
from collections import defaultdict
from pathlib import Path

TSV = Path("proof/pvp/cast/skill_cast_fields.tsv")
KEYS = ["nCostRage", "nAddRage", "nNeedRage", "nCostEnergy", "nNeedEnergy",
        "nCostSunEnergy", "nNeedSunEnergy", "nCostMoonEnergy", "nNeedMoonEnergy",
        "bIsAccumulate", "nNeedAccumulateCount", "nCostManaBasePercent",
        "nCostLife", "nCostStamina", "nCostSprintPower", "nCostMana"]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    only = sys.argv[1] if len(sys.argv) > 1 else None
    rows = [l.rstrip("\n").split("\t") for l in TSV.open(encoding="utf-8")][1:]
    per_school: dict[str, dict[str, list]] = defaultdict(lambda: defaultdict(list))
    for r in rows:
        if r[2] != "plain":
            continue
        if only and only != r[1]:
            continue
        f = json.loads(r[3])
        for k in KEYS:
            v = f.get(k)
            if v and v not in ("0", "false", "0.0"):
                per_school[r[1]][k].append((r[0], v))
    for school in sorted(per_school):
        print(f"===== {school}")
        for k in KEYS:
            vals = per_school[school].get(k)
            if not vals:
                continue
            print(f"  {k} ({len(vals)}):")
            for script, v in vals[:6]:
                print(f"      {script} = {v[:70]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
