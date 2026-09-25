#!/usr/bin/env python3
"""Print selected keyword sections of pvp_keyword_buffs.tsv compactly.

Usage: python tools/pvp/pvp_buff_view.py <pvp_keyword_buffs.tsv> [keyword ...] [--full]
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

TAB = Path(sys.argv[1])
args = [a for a in sys.argv[2:] if not a.startswith("--")]
full = "--full" in sys.argv
WANT = args or ["减疗", "禁疗", "无敌", "免伤", "免控", "反伤", "反弹", "破防",
                "化劲", "御劲", "重伤", "重创", "名剑", "竞技", "战场", "绝境"]

lines = TAB.read_text(encoding="utf-8").splitlines()
hdr = lines[0].split("\t")
print("COLS:", " | ".join(hdr))
for kw in WANT:
    print(f"===== {kw}")
    n = 0
    for r in lines[1:]:
        c = r.split("\t")
        if c[0] != kw:
            continue
        row = [
            f"ID={c[1]}", f"Name={c[2]}", f"FT={c[3]}", f"DT={c[4]}",
            f"MS={c[5]}", f"MS2={c[6]}", f"Ban={c[7]}", f"Inv={c[8]}",
            f"A1={c[9]}:{c[10]}", f"Count={c[11]}", f"Int={c[12]}", f"Stack={c[13]}",
        ]
        if full:
            row.append(f"Script={c[14]}")
        print("  " + " ".join(row))
        n += 1
        if n >= 200:
            break
    if n == 0:
        print("  (none)")
