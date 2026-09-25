#!/usr/bin/env python3
"""Search attribute names in attribute_raw.tsv and buff_attr_names.tsv."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ATTRS = Path("proof/pvp/attributes/attribute_raw.tsv")
BUFF_ATTRS = Path("proof/pvp/attributes/buff_attr_names.tsv")
KEYS = sys.argv[1:] or [
    "Rage", "Mana", "Energy", "Sun", "Moon", "Qi", "Stamina", "SprintPower",
    "Sword", "Dance", "Star", "Spirit", "Vigor", "Thew", "Bone", "Hate",
    "ClearCoolDown", "Haste", "OverDraft", "CoolDown", "TalentRecipe", "Accumulate",
]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    print("== attribute_raw.tsv (Attribute.txt)")
    for line in ATTRS.read_text(encoding="utf-8").splitlines():
        first = line.split("\t")[0]
        if any(k.lower() in first.lower() for k in KEYS):
            m = re.search(r'text="([^"]+)"', line)
            print(f"  {first:<42} {(m.group(1) if m else '')[:90]}")
    print("\n== buff_attr_names.tsv (Buff.tab attribute usage counts)")
    for line in BUFF_ATTRS.read_text(encoding="utf-8").splitlines():
        parts = line.split("\t")
        if len(parts) == 2 and any(k.lower() in parts[0].lower() for k in KEYS):
            print(f"  {parts[0]:<42} {parts[1]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
