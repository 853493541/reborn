#!/usr/bin/env python3
"""Count resource phrases in skill tooltips (Skill.txt) per skill school."""
from __future__ import annotations

import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

SKILLTXT = Path(
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4"
    r"\jx3-web-map-viewer\cache-extraction\pakv4-probe"
    r"\ad-desc-probe-out\ui\Scheme\Case\Skill.txt"
)
PHRASES = ["墨意", "剑气", "怒气", "禅那", "气点", "日灵", "月魂", "神机值", "剑舞",
           "连击", "星运", "刀气", "破绽", "药性", "战意", "能量", "内力", "气力值",
           "驭兽", "符印", "魂灯", "灵力", "剑意", "飞剑", "醉意", "刀魂"]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    text = SKILLTXT.read_bytes().decode("gb18030", errors="replace")
    rows = [l.split("\t") for l in text.splitlines()]
    per_skill: dict[str, Counter] = defaultdict(Counter)
    examples: dict[str, dict[str, str]] = defaultdict(dict)
    for r in rows:
        if len(r) < 13:
            continue
        sid, lvl, name = r[0], r[1], r[11]
        if lvl != "0":
            continue
        desc = " ".join(r[12:])
        for ph in PHRASES:
            if ph in desc:
                per_skill[sid][ph] += desc.count(ph)
                if ph not in examples[sid]:
                    m = re.search(r".{0,14}" + ph + r".{0,20}", desc)
                    examples[sid][ph] = m.group(0) if m else ""
    agg = Counter()
    for sid, cnt in per_skill.items():
        for ph, c in cnt.items():
            agg[ph] += 1
    print("phrase -> #skills mentioning")
    for ph, c in agg.most_common():
        print(f"  {ph:<8} {c}")
    print()
    for ph in [p for p, _ in agg.most_common(10)]:
        print(f"== {ph} examples")
        n = 0
        for sid, cnt in per_skill.items():
            if ph in cnt:
                print(f"   skill {sid:<7} {examples[sid][ph]}")
                n += 1
                if n >= 4:
                    break
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
