#!/usr/bin/env python3
"""Analyze hit-stiff / beat-back / beat-break columns in skills.tab."""
from __future__ import annotations

import sys
from collections import Counter
from pathlib import Path

SKILLS = Path(
    r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\SeasunDownloaderV2.4"
    r"\jx3-web-map-viewer\cache-extraction\pakv4-probe"
    r"\logic-skill-prefixed-out\settings\skill\skills.tab"
)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    text = SKILLS.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    hdr = rows[0]

    def c(r, i):
        return r[i] if i < len(r) else ""

    cols = ["CauseBeatBreak", "CauseBeatBack", "HasCriticalStrike", "HitStiffDelayFrame",
            "HitStiffSkillMoveID", "HitStiffVelocityXY", "HitStiffAccelerateXY"]
    for name in cols:
        i = hdr.index(name)
        d = Counter(c(r, i) for r in rows[1:])
        nonzero = {k: v for k, v in d.items() if k not in ("", "0")}
        print(f"-- {name}[{i}] nonzero distinct={len(nonzero)} total={sum(nonzero.values())}")
        print(f"   top: {sorted(nonzero.items(), key=lambda x: -x[1])[:18]}")

    print("\n== rows with any HitStiff* set")
    idx = {n: hdr.index(n) for n in cols}
    n = 0
    for r in rows[1:]:
        if c(r, idx["HitStiffSkillMoveID"]) or c(r, idx["HitStiffDelayFrame"]) or c(r, idx["HitStiffVelocityXY"]):
            print(f"  {c(r,1):>8} {c(r,0)[:26]:<28} kind={c(r,5):<12} delay={c(r,idx['HitStiffDelayFrame']):<5} "
                  f"moveID={c(r,idx['HitStiffSkillMoveID']):<7} vel={c(r,idx['HitStiffVelocityXY']):<10} "
                  f"acc={c(r,idx['HitStiffAccelerateXY']):<8} beatback={c(r,idx['CauseBeatBack'])} "
                  f"beatbreak={c(r,idx['CauseBeatBreak'])}")
            n += 1
            if n >= 40:
                break

    print("\n== CauseBeatBack nonzero examples")
    n = 0
    for r in rows[1:]:
        if c(r, idx["CauseBeatBack"]) not in ("", "0"):
            print(f"  {c(r,1):>8} {c(r,0)[:30]:<32} func={c(r,6):<10} kind={c(r,5):<12} "
                  f"beatbreak={c(r,idx['CauseBeatBreak'])} crit={c(r,idx['HasCriticalStrike'])}")
            n += 1
            if n >= 20:
                break
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
