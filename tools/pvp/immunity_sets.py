#!/usr/bin/env python3
"""Show control-relevant attributes per buff ID (immunity / halt / freeze / entrap ...).

Usage: python tools/pvp/immunity_sets.py <Buff.tab> [--ids a,b,c] [--match 疾如风]
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ap = argparse.ArgumentParser()
ap.add_argument("table", type=Path)
ap.add_argument("--ids", default="")
ap.add_argument("--match", default="")
ap.add_argument("--limit", type=int, default=120)
args = ap.parse_args()

text = args.table.read_bytes().decode("gb18030", errors="replace")
rows = [r.split("\t") for r in text.splitlines() if r.strip()]
hdr = rows[0]
col = {h: i for i, h in enumerate(hdr)}
ATTRS = [h for h in hdr if h.startswith(("BeginAttrib", "ActiveAttrib", "EndTimeAttrib"))]
want = {x for x in args.ids.split(",") if x}

interesting = ("atImmunity", "atHalt", "atFreeze", "atEntrap", "atSilence", "atSilenceAll",
               "atDisarm", "atFear", "atKnockedDownRate", "atKnockedBackRate",
               "atRepulsedRate", "atPullRate", "atGlobalBlock", "atNegativeShield",
               "atChaos", "atBlind")

seen: set[tuple[str, str]] = set()
n = 0
for r in rows[1:]:
    if want and r[0] not in want:
        continue
    if args.match and args.match not in (r[1] if len(r) > 1 else ""):
        continue
    pairs = []
    for a in ATTRS:
        i = col[a]
        if i >= len(r) or r[i] not in interesting:
            continue
        # value A is next column
        v = r[i + 1] if i + 1 < len(r) else ""
        pairs.append(f"{r[i]}={v}")
    if not pairs:
        continue
    key = (r[0], " ".join(pairs))
    if key in seen:
        continue
    seen.add(key)
    lvl = r[col.get("Level", 9)] if "Level" in col else ""
    print(f"{r[0]}\t{r[1]}\tlvl={lvl}\t{' '.join(pairs)}")
    n += 1
    if n >= args.limit:
        break
print(f"# rows={n}", file=sys.stderr)
