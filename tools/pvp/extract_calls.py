#!/usr/bin/env python3
"""Extract AddAttribute calls of control/dispel interest with args+comments.

Usage: python tools/pvp/extract_calls.py <scripts_root> <out.txt>
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

root = Path(sys.argv[1])
out_path = Path(sys.argv[2])
TOKENS = [
    "DEL_MULTI_GROUP_BUFF_BY_FUNCTIONTYPE",
    "DETACH_MULTI_GROUP_BUFF",
    "DEL_SINGLE_BUFF_BY_ID",
    "DEL_SINGLE_BUFF_BY_ID_AND_LEVEL",
    "AddSlowCheckSelfBuff",
    "AddSlowCheckDestBuff",
    "CALL_BUFF",
    "BindBuff",
    "DEL_MULTI_GROUP_BUFF_BY_DECAYTYPE",
    "DEL_MULTI_GROUP_BUFF_BY_MOVESTATE",
]

out: list[str] = []
for f in sorted(root.rglob("*.lua")):
    raw = f.read_bytes()
    if raw[:4] == b"\x1bLua":
        continue
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError:
        text = raw.decode("gb18030", errors="replace")
    lines = text.splitlines()
    rel = str(f.relative_to(root))
    for i, line in enumerate(lines):
        for tok in TOKENS:
            if tok in line and "ATTRIBUTE_TYPE." + tok in line:
                # gather next 3 non-empty lines
                args = []
                j = i + 1
                while j < len(lines) and len(args) < 3:
                    s = lines[j].strip()
                    if s and not s.startswith(")"):
                        args.append(s)
                    j += 1
                out.append(f"{rel}:{i+1}\t{tok}\t{' '.join(args)[:200]}")
                break
out_path.write_text("\n".join(out), encoding="utf-8")
print(f"written {out_path} lines={len(out)}")
