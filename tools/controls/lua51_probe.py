#!/usr/bin/env python3
"""Instruction-level Lua 5.1 bytecode probe (JX3: int=4 size_t=4 number=8).

Prints function prototypes with decoded opcodes and resolved constants.
Useful to settle exact UI-script behaviour (camera slider mapping, hotkey
save/conflict logic) that the constants/globals dumper does not show.

Usage:
  python tools/controls/lua51_probe.py FILE --string WidAngle
  python tools/controls/lua51_probe.py FILE --index 0/5
  python tools/controls/lua51_probe.py FILE --string X --out out.txt
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "netcode"))
from lua51_dump import Reader, read_proto, OPCODES  # noqa: E402


def rk(p: dict, x: int) -> str:
    consts = p["consts"]
    if x & 0x100:
        i = x & 0xFF
        if i < len(consts):
            c = consts[i]
            return f"K{i}({c!r})" if not isinstance(c, str) else f"K{i}({c!r})"
        return f"K{i}(?)"
    return f"R{x}"


def decode(p: dict, ins: int, pc: int) -> str:
    op = ins & 0x3F
    a = (ins >> 6) & 0xFF
    c = (ins >> 14) & 0x1FF
    b = (ins >> 23) & 0x1FF
    bx = (ins >> 14) & 0x3FFFF
    sbx = bx - 131071
    name = OPCODES[op]
    consts = p["consts"]
    line = p["lineinfo"][pc] if pc < len(p["lineinfo"]) else 0
    ann = ""
    if name in ("LOADK",):
        ann = repr(consts[bx]) if bx < len(consts) else "?"
    elif name in ("GETGLOBAL", "SETGLOBAL"):
        ann = repr(consts[bx]) if bx < len(consts) else "?"
    elif name == "CLOSURE":
        ann = f"proto {bx}"
    elif name in ("GETTABLE", "SETTABLE", "SELF", "ADD", "SUB", "MUL", "DIV",
                  "MOD", "POW", "EQ", "LT", "LE"):
        ann = f"{rk(p, b)} {rk(p, c)}"
    elif name in ("JMP", "FORLOOP", "FORPREP"):
        ann = f"to pc {pc + 1 + sbx}"
    elif name == "CALL":
        ann = f"args={b - 1} ret={c - 1}"
    return f"pc={pc:4d} L{line:<5d} {name:<9} A={a:<3} B={b:<4} C={c:<4} {ann}"


def matches(p: dict, needle: str) -> bool:
    return any(isinstance(c, str) and needle in c for c in p["consts"])


def walk(p: dict, path: str, out: list, needle: str, only: str, depth: int):
    if only and path == only or not only and matches(p, needle):
        out.append(f"\n===== function {path} | line {p['line_defined']}-{p['last_line']} "
                   f"| params={p['numparams']} nups={p['nups']} stack={p['maxstack']} =====")
        if p["upvalue_names"]:
            out.append(f"upvalues: {p['upvalue_names']}")
        for pc, ins in enumerate(p["code"]):
            out.append(decode(p, ins, pc))
    for i, sub in enumerate(p["protos"]):
        walk(sub, f"{path}/{i}", out, needle, only, depth + 1)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("path", type=Path)
    ap.add_argument("--string", default="")
    ap.add_argument("--index", default="")
    ap.add_argument("--out", type=Path)
    args = ap.parse_args()

    data = args.path.read_bytes()
    r = Reader(data, little=True, size_t=4, int_size=4, number_size=8)
    if data[0:4] != b"\x1bLua":
        print("not Lua bytecode")
        return 1
    r.take(12)   # signature + version/format/endianness/sizes/integral flag
    proto = read_proto(r)
    out: list[str] = []
    walk(proto, "0", out, args.string, args.index, 0)
    text = "\n".join(out) + "\n"
    if args.out:
        args.out.write_text(text, encoding="utf-8")
        print(f"{len(out)} lines -> {args.out}")
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
