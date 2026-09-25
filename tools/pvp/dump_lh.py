#!/usr/bin/env python3
"""Dump all string/number constants from Lua5.1 bytecode files into a flat UTF-8 report.

Usage: python tools/pvp/dump_lh.py <out.txt> <file1> [file2 ...]
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "netcode"))
import lua51_constants  # noqa: E402


def _string_utf8(self):
    n = self.size()
    if n == 0:
        return None
    raw = self.read(n)[:-1]
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw.decode("gb18030", errors="replace")


lua51_constants.Reader.string = _string_utf8

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

out_path = Path(sys.argv[1])
lines: list[str] = []
for fp in sys.argv[2:]:
    p = Path(fp)
    res = lua51_constants.dump(p, max_depth=64)
    lines.append(f"\n{'='*100}\n# FILE {p} size={p.stat().st_size}\n{'='*100}")
    if res is None:
        lines.append("# NOT Lua bytecode")
        continue
    lines.append(f"# consumed={res['consumed']}/{res['size']} funcs={len(res['functions'])} error={res['error']}")
    for fn in res["functions"]:
        lines.append(f"-- fn d{fn['depth']} src={fn['source']} params={fn['params']}")
        for s in fn["strings"]:
            if s:
                lines.append(f"  S {s}")
        for n in fn["numbers"]:
            lines.append(f"  N {n!r}")
out_path.write_text("\n".join(lines), encoding="utf-8")
print(f"written {out_path} lines={len(lines)}")
