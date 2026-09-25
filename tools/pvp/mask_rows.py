#!/usr/bin/env python3
"""Sample Buff.tab rows for a given mask column value.

Usage: python tools/pvp/mask_rows.py <Buff.tab> <column> <value> [limit]
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

TAB, COL, VAL = Path(sys.argv[1]), sys.argv[2], sys.argv[3]

LIMIT = int(sys.argv[4]) if len(sys.argv) > 4 else 15

text = TAB.read_bytes().decode("gb18030", errors="replace")
rows = [r.split("\t") for r in text.splitlines() if r.strip()]
hdr = rows[0]
col = {h: i for i, h in enumerate(hdr)}


def c(r, name):
    i = col[name]
    return r[i] if i < len(r) else ""


print(f"# {COL}={VAL} samples")
for r in rows[1:]:
    if c(r, COL) != VAL:
        continue
    print(f"{r[0]:>7}\t{c(r,'Name')[:46]}\tUseage={c(r,'Useage')}\tFT={c(r,'FunctionType')}\t"
          f"MS={c(r,'MoveStateMask')}\tMS2={c(r,'MoveStateMask2')}\tMapBan={c(r,'MapBanMask')}\t"
          f"MapInv={c(r,'MapInvalidMask')}\tA1={c(r,'BeginAttrib1')}:{c(r,'BeginValue1A')}")
    LIMIT -= 1
    if LIMIT <= 0:
        break
