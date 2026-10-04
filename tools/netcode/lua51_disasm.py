#!/usr/bin/env python3
"""Disassemble JX3 Lua 5.1 bytecode prototypes (32-bit build), filtered by constant.

Usage:
  python tools/netcode/lua51_disasm.py FILE --grep CorrectServerByIP [--depth N]
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
    "TEST", "TESTSET", "CALL", "TAILCALL", "RETURN", "FORLOOP", "FORPREP",
    "TFORLOOP", "SETLIST", "CLOSE", "CLOSURE", "VARARG",
]


class Reader:
    def __init__(self, data, int_size, size_size, num_size):
        self.data = data
        self.pos = 0
        self.int_size = int_size
        self.size_size = size_size
        self.num_size = num_size

    def read(self, n):
        if self.pos + n > len(self.data):
            raise EOFError("eof")
        c = self.data[self.pos:self.pos + n]
        self.pos += n
        return c

    def u8(self):
        return self.read(1)[0]

    def i32(self):
        return int.from_bytes(self.read(self.int_size), "little", signed=True)

    def size(self):
        return int.from_bytes(self.read(self.size_size), "little")

    def number(self):
        raw = self.read(self.num_size)
        return struct.unpack("<d", raw)[0] if self.num_size == 8 else 0.0

    def string(self):
        n = self.size()
        if n == 0:
            return None
        return self.read(n)[:-1].decode("gb18030", errors="replace")


def parse_function(r, depth, max_depth, out, path):
    source = r.string()
    r.i32()   # linedefined
    r.i32()   # lastlinedefined
    r.u8()    # nups
    r.u8()    # numparams
    r.u8()    # is_vararg
    r.u8()    # maxstacksize
    ncode = r.size()
    code = [struct.unpack("<I", r.read(4))[0] for _ in range(ncode)]
    nconst = r.size()
    consts = []
    for _ in range(nconst):
        t = r.u8()
        if t == 0:
            consts.append(None)
        elif t == 1:
            consts.append(bool(r.u8()))
        elif t == 3:
            consts.append(r.number())
        elif t == 4:
            consts.append(r.string())
        else:
            raise ValueError("bad const type %d" % t)
    nprotos = r.size()
    fn = {"path": path, "code": code, "consts": consts, "children": [], "source": source}
    out.append(fn)
    for i in range(nprotos):
        fn["children"].append(parse_function(r, depth + 1, max_depth, out, path + [i]))
    # debug
    nline = r.size()
    for _ in range(nline):
        r.i32()
    nloc = r.size()
    for _ in range(nloc):
        r.string()
        r.i32()
        r.i32()
    nup = r.size()
    for _ in range(nup):
        r.string()
    return fn


def const_str(fn, idx):
    if 0 <= idx < len(fn["consts"]):
        c = fn["consts"][idx]
        return repr(c)
    return "?"


def disasm(fn, all_fns):
    print("=== function %s source=%r code=%d consts=%d ===" % (fn["path"], fn["source"], len(fn["code"]), len(fn["consts"])))
    for i, c in enumerate(fn["consts"][:40]):
        if isinstance(c, str) or isinstance(c, (int, float)):
            print("   K%-3d %r" % (i, c))
    for i, ins in enumerate(fn["code"]):
        op = ins & 0x3F
        a = (ins >> 6) & 0xFF
        c = (ins >> 14) & 0x1FF
        b = (ins >> 23) & 0x1FF
        bx = ((ins >> 23) & 0x1FF) << 9 | c
        sbx = bx - 131071
        name = OPCODES[op] if op < len(OPCODES) else "OP%d" % op
        extra = ""
        def rk(v):
            if v >= 256:
                return "K%u=%s" % (v - 256, const_str(fn, v - 256))
            return "R%u" % v
        if name in ("LOADK", "GETGLOBAL", "SETGLOBAL"):
            extra = " K%u=%s" % (bx, const_str(fn, bx))
        elif name == "GETUPVAL":
            extra = " U%u" % bx
        elif name == "GETTABLE":
            extra = " K%u=%s" % (c, const_str(fn, c))
        elif name == "SETTABLE":
            extra = " K%u=%s" % (b, const_str(fn, b))
        elif name == "SELF":
            extra = " K%u=%s" % (c, const_str(fn, c))
        elif name == "CLOSURE":
            extra = " P%u" % bx
        elif name in ("JMP",):
            extra = " -> %d" % (i + 1 + sbx)
        elif name in ("EQ", "LT", "LE", "TEST", "TESTSET"):
            extra = " %s %s" % (rk(b), rk(c))
        elif name in ("LOADBOOL",):
            extra = " B=%u C=%u" % (b, c)
        elif name in ("CALL", "TAILCALL"):
            extra = " nargs=%u nres=%u" % (b - 1, c - 1)
        elif name == "RETURN":
            extra = " nres=%u" % (b - 1)
        elif name in ("ADD", "SUB", "MUL", "DIV", "MOD", "POW", "CONCAT"):
            extra = " K%u=%s" % (c, const_str(fn, c))
        print("  %3d  %-10s A=%-3d B=%-3d C=%-3d%s" % (i, name, a, b, c, extra))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("file", type=Path)
    ap.add_argument("--grep", default="")
    ap.add_argument("--max", type=int, default=3)
    args = ap.parse_args()
    data = args.file.read_bytes()
    if data[:4] != b"\x1bLua":
        print("not lua bytecode")
        return 1
    r = Reader(data, 4, 4, 8)
    r.read(4)
    r.u8()
    r.u8()
    r.u8()
    int_size = r.u8()
    size_size = r.u8()
    r.u8()
    num_size = r.u8()
    r.u8()
    r = Reader(data, int_size, size_size, num_size)
    r.read(12)
    out = []
    parse_function(r, 0, 3, out, [0])
    flat = []

    def walk(fn):
        flat.append(fn)
        for ch in fn["children"]:
            walk(ch)
    walk(out[0])
    print("total functions: %d" % len(flat))
    if args.grep:
        hits = [f for f in flat if any(isinstance(c, str) and args.grep in c for c in f["consts"])]
        print("functions containing %r: %d" % (args.grep, len(hits)))
        for f in hits[:args.max]:
            disasm(f, flat)
    return 0


if __name__ == "__main__":
    sys.exit(main())
