#!/usr/bin/env python3
"""Per-DetachType group listing from Buff.tab (sample names + FunctionType mix).

Usage: python tools/pvp/detach_groups.py <Buff.tab> <out.txt>
"""
from __future__ import annotations

import collections
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

TAB, OUT = Path(sys.argv[1]), Path(sys.argv[2])
text = TAB.read_bytes().decode("gb18030", errors="replace")
rows = [r.split("\t") for r in text.splitlines() if r.strip()]
hdr = rows[0]
col = {h: i for i, h in enumerate(hdr)}
data = rows[1:]


def c(r, name):
    i = col[name]
    return r[i] if i < len(r) else ""


lines = ["# DetachType group listing (Buff.tab)"]
for dt in sorted({c(r, "DetachType") for r in data}, key=lambda x: int(x) if x.lstrip("-").isdigit() else 9999):
    sel = [r for r in data if c(r, "DetachType") == dt]
    fts = collections.Counter(c(r, "FunctionType") for r in sel)
    names = []
    for r in sel:
        nm = f"{r[0]}:{c(r, 'Name')}"
        if nm not in names:
            names.append(nm)
    lines.append("")
    lines.append(f"== DetachType={dt} rows={len(sel)} uniq={len(names)} FT={dict(fts)}")
    lines.append("   " + " ; ".join(names[:40]))

OUT.write_text("\n".join(lines), encoding="utf-8")
print(f"written {OUT} lines={len(lines)}")
