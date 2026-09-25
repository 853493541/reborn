#!/usr/bin/env python3
"""Grep plaintext Lua scripts with context around a token.

Usage: python tools/pvp/grep_ctx.py <scripts_root> <token> [before] [after]
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

root = Path(sys.argv[1])
token = sys.argv[2]
before = int(sys.argv[3]) if len(sys.argv) > 3 else 2
after = int(sys.argv[4]) if len(sys.argv) > 4 else 12

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
        if token in line:
            out.append(f"\n===== {rel}:{i+1}")
            for j in range(max(0, i - before), min(len(lines), i + after + 1)):
                out.append(f"{j+1:5d}| {lines[j]}")
Path(sys.argv[5]).write_text("\n".join(out), encoding="utf-8") if len(sys.argv) > 5 else print("\n".join(out))
print(f"# matches={sum(1 for l in out if l.startswith('====='))}")
