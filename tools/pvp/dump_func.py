#!/usr/bin/env python3
"""Dump one function's bytecode + constants from a JX3 Lua 5.1 chunk."""
from __future__ import annotations

import importlib.util
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
    want = [int(x) for x in sys.argv[2:]] if len(sys.argv) > 2 else []
    b = path.read_bytes()
    r = d.Reader(b)
    r.read(12)
    out: list = []
    d.parse_function(r, 0, out, 200)
    for fn, fpath in d.walk(out[0]):
        if want and fn["line"] not in want:
            continue
        print(f"==== FUNC {fpath} line={fn['line']} lastline={fn['lastline']} params={fn['params']}")
        for i, c in enumerate(fn["consts"]):
            print(f"  K[{i}] = {c!r}")
        for pc, instr in enumerate(fn["code"]):
            op, a, bb, c = d.decode(instr)
            bx = (instr >> 14) & 0x3FFFF
            s = f"{pc:5d} {d.OPNAMES[op]:10s} A={a} B={bb} C={c} Bx={bx}"
            if op == 1:
                s += f"  K[{bx}]={fn['consts'][bx]!r}"
            if op in (5, 7):
                s += f"  S[{bx}]={fn['consts'][bx]!r}"
            print(s)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
