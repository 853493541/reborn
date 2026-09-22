#!/usr/bin/env python3
"""Minimal Lua 5.1 bytecode dumper (no decompiler dependency).

Parses a Lua 5.1 chunk (JX3 scripts ship as `\\x1bLuaQ` bytecode) and prints each
function prototype with its constants, globals/calls (from GETGLOBAL/CALL ops)
and nested functions. Enough to reconstruct logic skeletons and pull IDs/rates.

Usage: python tools/netcode/lua51_dump.py FILE [--code]
"""
from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

OPCODES = [
    "MOVE", "LOADK", "LOADBOOL", "LOADNIL", "GETUPVAL", "GETGLOBAL", "GETTABLE",
    "SETGLOBAL", "SETUPVAL", "SETTABLE", "NEWTABLE", "SELF", "ADD", "SUB", "MUL",
    "DIV", "MOD", "POW", "UNM", "NOT", "LEN", "CONCAT", "JMP", "EQ", "LT", "LE",
    "TEST", "TESTSET", "CALL", "TAILCALL", "RETURN", "FORLOOP", "FORPREP", "TFORLOOP",
    "SETLIST", "CLOSE", "CLOSURE", "VARARG",
]


class Reader:
    def __init__(self, data: bytes, little: bool = True, size_t: int = 8, int_size: int = 4,
                 number_size: int = 8, integral: bool = False):
        self.d = data
        self.o = 0
        self.little = little
        self.size_t = size_t
        self.int_size = int_size
        self.number_size = number_size
        self.integral = integral

    def take(self, n: int) -> bytes:
        b = self.d[self.o:self.o + n]
        self.o += n
        return b

    def byte(self) -> int:
        return self.take(1)[0]

    def u32(self) -> int:
        return struct.unpack_from("<I" if self.little else ">I", self.take(4))[0]

    def size(self) -> int:
        raw = self.take(self.size_t)
        return int.from_bytes(raw, "little" if self.little else "big")

    def number(self) -> float:
        fmt = {4: "f", 8: "d"}[self.number_size]
        return struct.unpack_from("<" + fmt if self.little else ">" + fmt, self.take(self.number_size))[0]

    def string(self) -> str | None:
        n = self.size()
        if n == 0:
            return None
        raw = self.take(n)
        if raw.endswith(b"\x00"):
            raw = raw[:-1]
        return raw.decode("gb18030", errors="replace")


def read_proto(r: Reader) -> dict:
    p: dict = {}
    p["source"] = r.string()
    p["line_defined"] = r.u32()
    p["last_line"] = r.u32()
    p["nups"] = r.byte()
    p["numparams"] = r.byte()
    p["is_vararg"] = r.byte()
    p["maxstack"] = r.byte()
    n = r.u32()
    p["code"] = [r.u32() for _ in range(n)]
    n = r.u32()
    consts = []
    for _ in range(n):
        t = r.byte()
        if t == 0:
            consts.append(None)
        elif t == 1:
            consts.append(bool(r.byte()))
        elif t == 3:
            consts.append(r.number())
        elif t == 4:
            consts.append(r.string())
        else:
            consts.append(f"<bad const tag {t}>")
    p["consts"] = consts
    n = r.u32()
    p["protos"] = [read_proto(r) for _ in range(n)]
    n = r.u32()
    p["lineinfo"] = [r.u32() for _ in range(n)]
    n = r.u32()
    locvars = []
    for _ in range(n):
        locvars.append((r.string(), r.u32(), r.u32()))
    p["locvars"] = locvars
    n = r.u32()
    p["upvalue_names"] = [r.string() for _ in range(n)]
    return p


def dump(p: dict, depth: int = 0, show_code: bool = False) -> list[str]:
    pad = "  " * depth
    name = (p["source"] or "?").split("/")[-1].rstrip(".lua")
    lines = [f"{pad}function <{name}> line {p['line_defined']}-{p['last_line']} "
             f"params={p['numparams']} ups={p['nups']} stack={p['maxstack']}"]
    consts = p["consts"]
    strings = [c for c in consts if isinstance(c, str)]
    numbers = [c for c in consts if isinstance(c, (int, float)) and not isinstance(c, bool)]
    if strings:
        lines.append(f"{pad}  strings: " + ", ".join(repr(s)[:60] for s in strings[:20]))
    if numbers:
        lines.append(f"{pad}  numbers: " + ", ".join(str(round(n, 4)) for n in numbers[:20]))
    if p["upvalue_names"]:
        lines.append(f"{pad}  upvalues: {p['upvalue_names']}")
    if show_code:
        globals_used = []
        for ins in p["code"]:
            op = ins & 0x3F
            if OPCODES[op] == "GETGLOBAL" and op == 5:
                idx = (ins >> 14) & 0x3FFFF
                if idx < len(consts) and isinstance(consts[idx], str):
                    globals_used.append(consts[idx])
            if OPCODES[op] == "SETGLOBAL" and op == 7:
                idx = (ins >> 14) & 0x3FFFF
                if idx < len(consts) and isinstance(consts[idx], str):
                    globals_used.append("=" + consts[idx])
        if globals_used:
            lines.append(f"{pad}  globals: " + ", ".join(sorted(set(globals_used))[:40]))
    for sub in p["protos"]:
        lines += dump(sub, depth + 1, show_code)
    return lines


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("path", type=Path)
    ap.add_argument("--code", action="store_true", help="also list globals used")
    ap.add_argument("-o", "--out", type=Path)
    args = ap.parse_args(argv)

    data = args.path.read_bytes()
    if data[:4] != b"\x1bLua":
        print(f"not Lua bytecode: {data[:4]!r}")
        return 2
    version = data[4]
    fmt, little, int_size, size_t, instr_size, number_size = data[5], data[6], data[7], data[8], data[9], data[10]
    integral = data[11] if len(data) > 11 else 0
    r = Reader(data, little == 1, size_t, int_size, number_size, bool(integral))
    r.o = 12
    proto = read_proto(r)
    lines = [
        f"# {args.path.name}: Lua {version:#x} int={int_size} size_t={size_t} num={number_size}",
        *dump(proto, 0, args.code),
    ]
    text = "\n".join(lines) + "\n"
    if args.out:
        args.out.write_text(text, encoding="utf-8")
        print(f"{len(lines)} lines -> {args.out}")
    else:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
