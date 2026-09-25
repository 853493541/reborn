#!/usr/bin/env python3
"""Filter skills.tab and print chosen columns."""
from __future__ import annotations

import sys
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
    # filter key=value args
    conds = {}
    for a in sys.argv[1:]:
        k, _, v = a.partition("=")
        conds[k] = v
    cols = conds.pop("cols", "SkillName,SkillID,CastMode,IsChannelSkill,IsPassiveSkill,ScriptFile")
    colnames = cols.split(",")
    idxs = [hdr.index(c) for c in colnames]
    limit = int(conds.pop("limit", "40"))
    shown = 0
    print("\t".join(colnames))
    for r in rows[1:]:
        ok = True
        for k, v in conds.items():
            ci = hdr.index(k)
            cell = r[ci] if ci < len(r) else ""
            if v.startswith("!"):
                if cell == v[1:]:
                    ok = False
                    break
            elif v.startswith("~"):
                if v[1:].lower() not in cell.lower():
                    ok = False
                    break
            elif cell != v:
                ok = False
                break
        if not ok:
            continue
        print("\t".join((r[i] if i < len(r) else "") for i in idxs))
        shown += 1
        if shown >= limit:
            break
    print(f"# shown {shown}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
