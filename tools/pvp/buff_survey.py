#!/usr/bin/env python3
"""Survey Buff.tab enum-like columns: value counts + representative rows.

Usage: python tools/pvp/buff_survey.py <Buff.tab> <outdir>
"""
from __future__ import annotations

import collections
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

TAB = Path(sys.argv[1])
OUT = Path(sys.argv[2])
OUT.mkdir(parents=True, exist_ok=True)


def load(path: Path) -> tuple[list[str], list[list[str]]]:
    text = path.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    return rows[0], rows[1:]


header, rows = load(TAB)
col = {h: i for i, h in enumerate(header)}


def cell(r: list[str], name: str) -> str:
    i = col[name]
    return r[i] if i < len(r) else ""


COLS = [
    "Useage", "FunctionType", "BuffType", "AppendType", "DetachType",
    "DecayType", "MoveStateMask", "MoveStateMask2", "MapBanMask",
    "MapInvalidMask", "IsStackable", "IsCountable", "CanAccumulate",
    "IsIntensityStackable", "CanBeSteal", "CanTransfer", "Coexist",
    "CostSingleStack", "UniqueTarget", "Save", "OnFight", "Hide",
]

lines: list[str] = []
lines.append(f"# table={TAB} rows={len(rows)} cols={len(header)}")
for c in COLS:
    if c not in col:
        lines.append(f"\n## {c}: COLUMN MISSING")
        continue
    counter = collections.Counter(cell(r, c) for r in rows)
    lines.append(f"\n## {c}  distinct={len(counter)}")
    for val, n in counter.most_common():
        ex = next((r for r in rows if cell(r, c) == val), None)
        exs = f"  e.g. ID={ex[0]} Name={cell(ex, 'Name')}" if ex else ""
        lines.append(f"  {val!r}\t{n}{exs}")

(OUT / "buff_column_value_counts.txt").write_text("\n".join(lines), encoding="utf-8")
print("\n".join(lines[:200]))
print(f"\n# written {OUT / 'buff_column_value_counts.txt'}")
