#!/usr/bin/env python3
"""For each atImmunity value, list buff names (to infer control-category mapping)."""
from __future__ import annotations

import collections
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
text = Path(sys.argv[1]).read_bytes().decode("gb18030", errors="replace")
rows = [r.split("\t") for r in text.splitlines() if r.strip()]
hdr = rows[0]
col = {h: i for i, h in enumerate(hdr)}
att = [h for h in hdr if h.startswith(("BeginAttrib", "ActiveAttrib"))]

byval: dict[str, list[str]] = collections.defaultdict(list)
for r in rows[1:]:
    nm = r[1] if len(r) > 1 else ""
    for a in att:
        i = col[a]
        if i < len(r) and r[i] == "atImmunity":
            v = r[i + 1] if i + 1 < len(r) else ""
            byval[v].append(nm)

for k in sorted(byval, key=lambda x: int(x) if x.isdigit() else 999):
    names = byval[k]
    uniq = sorted(set(names))
    print(f"== atImmunity {k}  ({len(names)} refs, {len(uniq)} unique names)")
    for n in uniq[:40]:
        print("   ", n)
