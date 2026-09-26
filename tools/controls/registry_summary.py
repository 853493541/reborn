#!/usr/bin/env python3
"""Summarise the generated hotkey command registry (context groups/categories)."""
from __future__ import annotations

import collections
import csv
import io
import sys
from pathlib import Path

root = Path(__file__).resolve().parent.parent.parent
tsv = root / "proof" / "controls" / "hotkey_command_registry.tsv"
rows = list(csv.DictReader(io.open(tsv, encoding="utf-8"), delimiter="\t"))

out = []
out.append("commands: %d" % len(rows))
out.append("--- contextgroup values (UI grouping) ---")
for k, v in collections.Counter(r["contextgroup"] for r in rows).most_common():
    out.append("  %s  x%d" % (k or "(none)", v))
out.append("--- commands with up handlers: %d" % sum(1 for r in rows if r["up"]))
out.append("--- runOnUp commands ---")
for r in rows:
    if r["runOnUp"]:
        out.append("  %s: %s / %s" % (r["command"], r["down"], r["up"]))
out.append("--- sample down handler per group ---")
seen = set()
for r in rows:
    if r["contextgroup"] not in seen and r["down"]:
        seen.add(r["contextgroup"])
        out.append("  [%s] %s -> %s" % (r["contextgroup"] or "(none)", r["command"], r["down"]))

path = root / "proof" / "controls" / "registry_summary.txt"
path.write_text("\n".join(out) + "\n", encoding="utf-8")
print("wrote", path)
sys.exit(0)
