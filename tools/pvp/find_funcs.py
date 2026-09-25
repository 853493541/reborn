#!/usr/bin/env python3
"""Dump all functions in a Lua chunk whose constants/strings match a regex."""
from __future__ import annotations

import importlib.util
import re
import sys
from pathlib import Path

spec = importlib.util.spec_from_file_location(
    "d", str(Path(__file__).with_name("lua51_disasm.py"))
)
d = importlib.util.module_from_spec(spec)
spec.loader.exec_module(d)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    path = Path(sys.argv[1])
    pat = re.compile(sys.argv[2])
    b = path.read_bytes()
    r = d.Reader(b)
    r.read(12)
    out: list = []
    d.parse_function(r, 0, out, 200)
    for fn, fpath in d.walk(out[0]):
        strs = [c for c in fn["consts"] if isinstance(c, str)]
        if any(pat.search(s) for s in strs):
            print(f"==== FUNC {fpath} line={fn['line']} params={fn['params']}")
            print(f"  strings: {strs}")
            print(f"  numbers: {fn['numbers'][:40] if 'numbers' in fn else [c for c in fn['consts'] if isinstance(c,(int,float))][:40]}")
            for pc, instr in enumerate(fn["code"]):
                op, a, bb, c = d.decode(instr)
                bx = (instr >> 14) & 0x3FFFF
                s = f"  {pc:5d} {d.OPNAMES[op]:10s} A={a} B={bb} C={c} Bx={bx}"
                if op == 1:
                    s += f"  K[{bx}]={fn['consts'][bx]!r}"
                if op in (5, 7):
                    s += f"  S[{bx}]={fn['consts'][bx]!r}"
                print(s)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
