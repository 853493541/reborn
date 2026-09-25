#!/usr/bin/env python3
"""Dump value -> count -> example buffs for selected Buff.tab columns.

Usage: python tools/pvp/field_semantics.py <Buff.tab> <out.txt> [col ...]
"""
from __future__ import annotations

import collections
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

TAB, OUT = Path(sys.argv[1]), Path(sys.argv[2])
COLS = sys.argv[3:] or ["Useage", "FunctionType", "BuffType", "AppendType", "DetachType"]

text = TAB.read_bytes().decode("gb18030", errors="replace")
rows = [r.split("\t") for r in text.splitlines() if r.strip()]
hdr = rows[0]
col = {h: i for i, h in enumerate(hdr)}
data = rows[1:]

lines = [f"# Buff.tab rows={len(data)} cols={len(hdr)}", f"# source: {TAB}"]
for f in COLS:
    if f not in col:
        lines.append(f"== {f}: COLUMN NOT FOUND")
        continue
    i = col[f]
    cnt = collections.Counter((r[i] if i < len(r) else "") for r in data)
    # example per value: first non-empty name whose ID repeats fewest times
    examples: dict[str, list[str]] = collections.defaultdict(list)
    for r in data:
        k = r[i] if i < len(r) else ""
        nm = f"{r[0]}:{r[1]}"
        if len(examples[k]) < 5 and nm not in examples[k]:
            examples[k].append(nm)
    lines.append("")
    lines.append(f"== {f} distinct={len(cnt)}")
    for k, v in cnt.most_common():
        ex = " ; ".join(examples[k])
        lines.append(f"{k!r}\t{v}\t{ex}")

OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text("\n".join(lines), encoding="utf-8")
print(f"written {OUT} lines={len(lines)}")
