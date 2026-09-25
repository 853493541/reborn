#!/usr/bin/env python3
"""Print cast/channel timing assignments for named skill scripts."""
from __future__ import annotations

import os
import re
import sys

ROOT = (
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4"
    r"\jx3-web-map-viewer\cache-extraction\pakv4-probe"
    r"\ability-matcher\extracted\scripts\skill"
)
PAT = re.compile(
    r"skill\.(nPrepareFrames|nMinPrepareFrames|nChannelFrame|nMinChannelFrame|"
    r"nChannelInterval|nMinChannelInterval|bInstantChannel|bIgnorePrepareState)"
    r"\s*=\s*([^;\r\n]*)"
)
NAMES = sys.argv[1:] or [
    "兰摧玉折", "快雪时晴", "回雪飘摇", "玳弦急曲", "四象轮回", "阳明指",
    "引窍", "截阳", "蝎心", "蛇影", "夺魄箭", "追命箭", "徵", "长清",
    "商阳指", "风来吴山", "龙牙", "韦陀献杵",
]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    for nm in NAMES:
        for dirpath, _dirs, files in os.walk(ROOT):
            if "npc" in dirpath or "Quest" in dirpath or "沙漠风暴" in dirpath:
                continue
            for fn in files:
                if nm not in fn or not fn.lower().endswith(".lua"):
                    continue
                p = os.path.join(dirpath, fn)
                rel = os.path.relpath(p, ROOT)
                b = open(p, "rb").read()
                if b[:4] == b"\x1bLua":
                    print(f"{nm}: [BYTECODE] {rel}")
                    continue
                t = b.decode("gb18030", errors="replace")
                hits = PAT.findall(t)
                if hits:
                    print(f"{nm}: {rel}")
                    for k, v in hits[:10]:
                        print(f"      {k} = {v.strip()[:90]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
