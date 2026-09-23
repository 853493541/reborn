#!/usr/bin/env python3
"""Query JX3 player-visible skill/buff text from the extracted UI databases.

Databases (GBK TSV):
  ui/Scheme/Case/Skill.txt   SkillID, Level, ..., Name, Desc, ShortDesc,
                             SpecialDesc, KungfuDesc, HelpDesc, ...
  ui/Scheme/Case/Buff.txt    buff descriptions

Descriptions contain markup placeholders resolved by the client at runtime:
  <SUB id level>, <BUFF id level>, <KUNGFU id level>, <TALENT id level>,
  <SKILL PhysicsDamage> / <SKILLEx {D0} {SkillPhysicsAP}> ...

Usage:
  python tools/netcode/query_skill_tooltip.py --db "<...>/ui/Scheme/Case/Skill.txt" 蹑云逐月 风来吴山
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path


def load_rows(path: Path) -> tuple[list[str], list[list[str]]]:
    text = path.read_bytes().decode("gb18030", errors="replace")
    lines = text.splitlines()
    header = lines[0].split("\t")
    rows = [ln.split("\t") for ln in lines[1:] if ln.strip()]
    return header, rows


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--db", type=Path, required=True)
    ap.add_argument("names", nargs="+")
    ap.add_argument("--max-level", type=int, default=0,
                    help="only rows with Level <= this (0 = all)")
    ap.add_argument("--out", type=Path)
    args = ap.parse_args(argv)

    header, rows = load_rows(args.db)
    idx = {h: i for i, h in enumerate(header)}
    for col in ("Name", "Desc"):
        if col not in idx:
            print(f"missing column {col}; header={header[:14]}...", file=sys.stderr)
            return 2

    out_lines = ["\t".join(header)]
    for row in rows:
        if len(row) < len(header):
            continue
        name = row[idx["Name"]]
        if not any(k in name for k in args.names):
            continue
        if args.max_level and row[idx["Level"]].isdigit() and int(row[idx["Level"]]) > args.max_level:
            continue
        out_lines.append("\t".join(row))

    text = "\n".join(out_lines) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8")
        print(f"{len(out_lines) - 1} rows -> {args.out}")
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
