#!/usr/bin/env python3
"""Report tSkillData resource fields per school from skill_cast_fields.tsv."""
from __future__ import annotations

import json
import sys
from collections import defaultdict
from pathlib import Path

TSV = Path("proof/pvp/cast/skill_cast_fields.tsv")
KEYS = ["nCostMana", "nCostManaMaxPercent", "nCostRage", "nAddRage", "nCostEnergy",
        "nAddEnergy", "nCostSunEnergy", "nAddSunEnergy", "nCostMoonEnergy",
        "nAddMoonEnergy", "nCostSprintPower", "nCostStamina", "nCostLife",
        "nCostItemType", "nCostItemIndex"]
SCHOOLS = sys.argv[1:] or [
    "天策", "万花", "纯阳", "七秀", "少林", "藏剑", "丐帮", "明教", "五毒", "唐门",
    "苍云", "长歌", "霸刀", "蓬莱", "凌雪阁", "衍天", "北天药宗", "刀宗", "万灵山庄",
    "段氏", "绝境战场", "沙漠风暴",
]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    rows = [l.rstrip("\n").split("\t") for l in TSV.open(encoding="utf-8")][1:]
    per: dict[str, dict[str, list]] = defaultdict(lambda: defaultdict(list))
    for r in rows:
        if r[2] != "plain" or r[1] not in SCHOOLS:
            continue
        levels = json.loads(r[4])
        if not levels:
            continue
        agg: dict[str, str] = {}
        for k in KEYS:
            vals = [lv[k] for lv in levels if k in lv]
            if vals and any(v.strip() not in ("0", "0.0") for v in vals):
                agg[k] = vals[0] + (f" .. {vals[-1]}" if len(vals) > 1 and vals[-1] != vals[0] else "")
        if agg:
            per[r[1]][r[0]] = agg
    for school in SCHOOLS:
        if school not in per:
            continue
        print(f"===== {school}")
        for script, agg in list(per[school].items())[:8]:
            print(f"  {script}")
            for k, v in agg.items():
                print(f"      {k} = {v[:90]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
