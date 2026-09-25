#!/usr/bin/env python3
"""Minimal Lua 5.1 disassembler for JX3 bytecode (size_t=4).

Recovers named global assignments (`NAME = <number>`) and constants passed to
selected function calls (e.g. skill.SetPublicCoolDown(N)). JX3 ships 32-bit
Lua 5.1 prototypes on x64 (Instruction=4, lua_Number=8, size_t=4).

Usage:
  python tools/pvp/lua51_disasm.py FILE --globals [--json OUT]
  python tools/pvp/lua51_disasm.py FILE --calls SetPublicCoolDown,SetNormalCoolDown
  python tools/pvp/lua51_disasm.py FILE --dump [--func NAME]
"""
from __future__ import annotations

import argparse
import json
import struct
import sys
from pathlib import Path

OPNAMES = [
    "MOVE", "LOADK", "LOADBOOL", "LOADNIL", "GETUPVAL", "GETGLOBAL", "GETTABLE",
    "SETGLOBAL", "SETUPVAL", "SETTABLE", "NEWTABLE", "SELF", "ADD", "SUB", "MUL",
    "DIV", "MOD", "POW", "UNM", "NOT", "LEN", "CONCAT", "JMP", "EQ", "LT", "LE",
    "TEST", "TESTSET", "CALL", "TAILCALL", "RETURN", "FORLOOP", "FORPREP",
    "TFORLOOP", "SETLIST", "CLOSE", "CLOSURE", "VARARG",
]


class Reader:
    def __init__(self, data: bytes) -> None:
        self.data = data
        self.pos = 0
        self.int_size = data[7]
        self.size_size = data[8]
        self.num_size = data[10]

    def read(self, n: int) -> bytes:
        if self.pos + n > len(self.data):
            raise EOFError("eof")
        c = self.data[self.pos:self.pos + n]
        self.pos += n
        return c

    def u8(self) -> int:
        return self.read(1)[0]

    def i32(self) -> int:
        return int.from_bytes(self.read(self.int_size), "little", signed=True)

    def size(self) -> int:
        return int.from_bytes(self.read(self.size_size), "little")

    def number(self) -> float:
        return struct.unpack("<d", self.read(self.num_size))[0]

    def string(self) -> str | None:
        n = self.size()
        if n == 0:
            return None
        return self.read(n)[:-1].decode("gb18030", errors="replace")


def parse_function(r: Reader, depth: int, out: list, max_depth: int) -> None:
    source = r.string()
    linedefined = r.i32()
    lastlinedefined = r.i32()
    r.u8()
    numparams = r.u8()
    is_vararg = r.u8()
    maxstack = r.u8()
    n_code = r.i32()
    code = list(struct.unpack(f"<{n_code}I", r.read(n_code * 4)))
    n_const = r.i32()
    consts: list = []
    for _ in range(n_const):
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
            raise ValueError(f"unknown const type {t}")
    fns = []
    out.append({
        "depth": depth,
        "source": source,
        "line": linedefined,
        "lastline": lastlinedefined,
        "params": numparams,
        "is_vararg": is_vararg,
        "maxstack": maxstack,
        "code": code,
        "consts": consts,
        "children": fns,
    })
    n_proto = r.i32()
    for _ in range(n_proto):
        if depth + 1 <= max_depth:
            parse_function(r, depth + 1, fns, max_depth)
        else:
            skip_function(r)
    r.read(r.i32() * 4)
    n_loc = r.i32()
    for _ in range(n_loc):
        r.string(); r.i32(); r.i32()
    for _ in range(r.i32()):
        r.string()


def skip_function(r: Reader) -> None:
    r.string()
    r.i32(); r.i32(); r.u8(); r.u8(); r.u8(); r.u8()
    r.read(r.i32() * 4)
    n = r.i32()
    for _ in range(n):
        t = r.u8()
        if t == 1:
            r.read(1)
        elif t == 3:
            r.read(r.num_size)
        elif t == 4:
            r.string()
    for _ in range(r.i32()):
        skip_function(r)
    r.read(r.i32() * 4)
    for _ in range(r.i32()):
        r.string(); r.i32(); r.i32()
    for _ in range(r.i32()):
        r.string()


def decode(instr: int) -> tuple[int, int, int, int]:
    op = instr & 0x3F
    a = (instr >> 6) & 0xFF
    c = (instr >> 14) & 0x1FF
    b = (instr >> 23) & 0x1FF
    bx = (instr >> 14) & 0x3FFFF
    return op, a, b, c


def walk(fn: dict, path: str = "main"):
    yield fn, path
    for i, ch in enumerate(fn["children"]):
        src = ch["source"] or "?"
        yield from walk(ch, f"{path}/{src}#{ch['line']}")


def analyze(fn: dict, path: str, globals_out: list, calls: dict[str, list], dump: list) -> None:
    code = fn["code"]
    consts = fn["consts"]
    regs: dict[int, tuple[str, object]] = {}
    for pc, instr in enumerate(code):
        op, a, b, c = decode(instr)
        bx = (instr >> 14) & 0x3FFFF
        if op == 1:  # LOADK
            regs[a] = ("K", consts[bx] if bx < len(consts) else None)
        elif op == 2:  # LOADBOOL
            regs[a] = ("K", bool(b))
        elif op == 5:  # GETGLOBAL
            regs[a] = ("G", consts[bx] if bx < len(consts) else None)
        elif op == 7:  # SETGLOBAL
            name = consts[bx] if bx < len(consts) else None
            val = regs.get(a)
            globals_out.append((path, pc, name, val[1] if val else None, val[0] if val else None))
        elif op == 6:  # GETTABLE R(A)=R(B)[RK(C)]
            key = consts[c - 256] if c >= 256 and c - 256 < len(consts) else (regs.get(c) or (None, None))[1]
            src = regs.get(b)
            regs[a] = ("T", f"{src[1] if src else '?'}.{key}")
        elif op == 28:  # CALL
            callee = regs.get(a)
            args = []
            for i in range(a + 1, a + b):
                v = regs.get(i)
                args.append(v[1] if v else "?")
            if callee and isinstance(callee[1], str):
                short = callee[1].split(".")[-1]
                if short in calls:
                    calls[short].append((path, pc, args, callee[1]))
        elif op == 0:  # MOVE
            regs[a] = regs.get(b, ("?", None))
        elif op in (12, 13, 14, 15, 16, 17):  # arith
            lhs = regs.get(b)
            rhs = consts[c - 256] if c >= 256 and c - 256 < len(consts) else (regs.get(c) or (None, None))[1]
            if lhs and isinstance(lhs[1], str) and isinstance(rhs, str) and rhs == "GAME_FPS":
                regs[a] = ("E", f"{lhs[1]}*{rhs}")
            else:
                regs[a] = ("?", None)
        elif op == 18:  # UNM
            regs[a] = ("?", None)
        else:
            if op in (9, 10, 11, 28, 29, 30, 31, 32, 33, 34, 36, 37, 8, 4):
                regs[a] = ("?", None)
        if dump:
            dump.append(f"{path}\t{pc}\t{OPNAMES[op] if op < len(OPNAMES) else op}\tA={a} B={b} C={c}")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("file", type=Path)
    ap.add_argument("--globals", action="store_true")
    ap.add_argument("--calls", default="")
    ap.add_argument("--dump", action="store_true")
    ap.add_argument("--json", type=Path)
    args = ap.parse_args()

    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    data = args.file.read_bytes()
    if data[:4] != b"\x1bLua":
        print("not lua bytecode", file=sys.stderr)
        return 1
    r = Reader(data)
    r.read(12)
    root: list = []
    parse_function(r, 0, root, 200)

    want = [s for s in args.calls.split(",") if s]
    globals_out: list = []
    calls: dict[str, list] = {k: [] for k in want}
    dump: list = []
    for fn, path in walk(root[0]):
        analyze(fn, path, globals_out, calls, dump)

    if args.globals:
        for path, pc, name, val, kind in globals_out:
            print(f"{name}\t{val!r}\t{kind}\t{path}\tpc={pc}")
    for k, rows in calls.items():
        print(f"== calls {k}: {len(rows)}")
        for path, pc, cargs, full in rows[:100]:
            print(f"   {path} pc={pc} args={cargs} callee={full}")
    if args.dump:
        print("\n".join(dump))
    if args.json:
        args.json.write_text(json.dumps({
            "file": str(args.file),
            "globals": [{"name": n, "value": v, "kind": k, "func": p, "pc": pc} for p, pc, n, v, k in globals_out],
            "calls": {k: [{"func": p, "pc": pc, "args": a, "callee": c} for p, pc, a, c in v] for k, v in calls.items()},
        }, ensure_ascii=False, indent=1), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
