#!/usr/bin/env python3
"""Find Buff.tab rows whose attribute slots use atSetTalentRecipe (talents/recipes)."""
from __future__ import annotations

import sys
from pathlib import Path

BUFF = Path(
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4"
    r"\jx3-web-map-viewer\cache-extraction\pakv4-probe"
    r"\logic-skill-prefixed-out\settings\skill\Buff.tab"
)
TARGET = sys.argv[1] if len(sys.argv) > 1 else "atSetTalentRecipe"
LIMIT = int(sys.argv[2]) if len(sys.argv) > 2 else 15


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    text = BUFF.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    hdr = rows[0]

    def c(r, i):
        return r[i] if i < len(r) else ""

    attr_cols = [i for i, h in enumerate(hdr) if h.startswith(("BeginAttrib", "ActiveAttrib", "EndTimeAttrib"))]
    val_cols = [i for i, h in enumerate(hdr) if h.startswith(("BeginValue", "ActiveValue", "EndTimeValue"))]
    n = 0
    for r in rows[1:]:
        hit = None
        for i in attr_cols:
            if c(r, i) == TARGET:
                hit = i
                break
        if hit is None:
            continue
        vals = []
        for j in range(hit, min(hit + 3, len(hdr))):
            vals.append(f"{hdr[j]}={c(r,j)}")
        print(f"buff {c(r,0):>7} {c(r,1)[:26]:<28} type={c(r,8):<4} "
              f"onKungfu={c(r,91):<7} script={c(r,23)[:38]:<40} {' '.join(vals)}")
        n += 1
        if n >= LIMIT:
            break
    print(f"# shown {n}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
