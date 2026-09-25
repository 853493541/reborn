#!/usr/bin/env python3
"""Dump selected Buff.tab columns for a list of buff IDs (or a Name substring).

Usage:
  python tools/pvp/buff_rows.py <Buff.tab> --ids 424,445,554 [--match 眩晕]
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from decode_movestate import IDX1, bits  # noqa: E402

COLS = [
    "ID", "Name", "Useage", "FunctionType", "AppendType", "DetachType",
    "BuffType", "Level", "Intensity", "IsStackable", "MaxStackNum", "Count",
    "Interval", "Exclude", "GlobalExclude", "UniqueTarget", "CanCancel",
    "IsCountable", "DecayType", "MoveStateMask", "MapBanMask", "CanAccumulate",
    "MapInvalidMask", "IsCombatBuff", "MinInterval", "MoveStateMask2",
    "MaxInterval", "ActiveCoefficient", "CanBeSteal", "NeedSync", "CanTransfer",
    "CostSingleStack", "Coexist", "IsIntensityStackable", "ScriptFile",
]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("table", type=Path)
    ap.add_argument("--ids", default="")
    ap.add_argument("--match", default="")
    ap.add_argument("--limit", type=int, default=200)
    ap.add_argument("--mask", action="store_true", help="decode MoveStateMask bits")
    args = ap.parse_args()

    text = args.table.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    header = rows[0]
    col = {h: i for i, h in enumerate(header)}
    wanted = {int(x) for x in args.ids.split(",") if x.strip()}

    def cell(r: list[str], name: str) -> str:
        i = col[name]
        return r[i] if i < len(r) else ""

    print("\t".join(COLS))
    n = 0
    for r in rows[1:]:
        rid = r[0]
        if wanted and rid not in {str(w) for w in wanted}:
            continue
        if args.match and args.match.lower() not in cell(r, "Name").lower():
            continue
        out = [cell(r, c) for c in COLS]
        if args.mask:
            try:
                v = int(cell(r, "MoveStateMask"))
                out.append("|".join(f"{i}:{IDX1.get(i, '?')}" for i in bits(v)))
            except ValueError:
                out.append("")
            try:
                v2 = int(cell(r, "MoveStateMask2"))
                out.append("|".join(f"{i}" for i in bits(v2)))
            except ValueError:
                out.append("")
        print("\t".join(out))
        n += 1
        if n >= args.limit:
            break
    print(f"# rows={n}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
