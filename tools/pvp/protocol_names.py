#!/usr/bin/env python3
"""Extract protocol struct-name constants (S2C_*, C2S_*, ...) from the net dumps."""
from __future__ import annotations

import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(r"C:\Users\Zhibin Ren\Desktop\reborn-pvp\proof\netcode")
FILES = [
    ROOT / "JX3ClientX64_exe_net_strings.txt",
    ROOT / "JX3LogicEditOperation_net_strings.txt",
    ROOT / "JX3RepresentX64_net_strings.txt",
    ROOT / "JX3UIX64_net_strings.txt",
    ROOT / "KBaseX64_net_strings.txt",
]
OUT = Path(r"C:\Users\Zhibin Ren\Desktop\reborn-pvp\proof\pvp\netcode\protocol_type_names.tsv")
PAT = re.compile(r"\b(?:C2S|S2C|G2C|R2C|L2C|SS2C|V2C|BFC)_[A-Z0-9_]+")

names: dict[str, list[tuple[str, str]]] = {}
for f in FILES:
    if not f.is_file():
        continue
    for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith("#"):
            continue
        parts = line.split("\t", 2)
        if len(parts) < 3:
            continue
        off, _enc, text = parts
        for m in PAT.findall(text):
            names.setdefault(m, []).append((f.name, off))

lines = ["name\tsource\toffset"]
for k in sorted(names):
    for src, off in names[k]:
        lines.append(f"{k}\t{src}\t{off}")
OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"{len(names)} unique protocol type names -> {OUT}")
for k in sorted(names):
    print(k)
