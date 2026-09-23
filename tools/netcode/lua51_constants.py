#!/usr/bin/env python3
"""Dump constants (numbers/strings) from JX3 Lua 5.1 bytecode.

JX3 ships skill scripts as a 32-bit Lua 5.1 build (size_t=4, Instruction=4,
lua_Number=8) on x64. This parser walks the full prototype tree and prints
every constant per function, so gameplay scaling values can be recovered from
compiled scripts too.

Usage:
  python tools/netcode/lua51_constants.py FILE [FILE...] [--json OUT] [--depth N]
"""
from __future__ import annotations

import argparse
import json
import struct
import sys
from pathlib import Path


class Reader:
    def __init__(self, data: bytes, int_size: int, size_size: int, num_size: int) -> None:
        self.data = data
        self.pos = 0
        self.int_size = int_size
        self.size_size = size_size
        self.num_size = num_size

    def read(self, n: int) -> bytes:
        if self.pos + n > len(self.data):
            raise EOFError("unexpected end of bytecode")
        chunk = self.data[self.pos:self.pos + n]
        self.pos += n
        return chunk

    def u8(self) -> int:
        return self.read(1)[0]

    def i32(self) -> int:
        return int.from_bytes(self.read(self.int_size), "little", signed=True)

    def size(self) -> int:
        return int.from_bytes(self.read(self.size_size), "little")

    def number(self) -> float:
        raw = self.read(self.num_size)
        if self.num_size == 8:
            return struct.unpack("<d", raw)[0]
        return 0.0

    def string(self) -> str | None:
        n = self.size()
        if n == 0:
            return None
        return self.read(n)[:-1].decode("gb18030", errors="replace")


def parse_function(r: Reader, depth: int, out: list, max_depth: int) -> None:
    source = r.string()
    r.i32()
    r.i32()
    r.u8()
    numparams = r.u8()
    r.u8()
    maxstack = r.u8()

    n_code = r.i32()
    r.read(n_code * 4)

    n_const = r.i32()
    consts = []
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
            raise ValueError(f"unknown constant type {t}")

    numbers = [c for c in consts if isinstance(c, (int, float)) and not isinstance(c, bool)]
    strings = [c for c in consts if isinstance(c, str)]
    out.append({
        "depth": depth,
        "source": source,
        "params": numparams,
        "maxstack": maxstack,
        "numbers": numbers,
        "strings": strings,
    })

    n_proto = r.i32()
    for _ in range(n_proto):
        if depth + 1 <= max_depth:
            parse_function(r, depth + 1, out, max_depth)
        else:
            skip_function(r)

    r.read(r.i32() * 4)          # lineinfo
    n_loc = r.i32()
    for _ in range(n_loc):
        r.string(); r.i32(); r.i32()
    n_up = r.i32()
    for _ in range(n_up):
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


def dump(path: Path, max_depth: int = 8) -> dict | None:
    data = path.read_bytes()
    if data[:4] != b"\x1bLua":
        return None
    int_size, size_size, num_size = data[7], data[8], data[10]
    r = Reader(data, int_size, size_size, num_size)
    r.read(12)  # full header: signature, version, format, endianness, sizes, integral
    out: list = []
    try:
        parse_function(r, 0, out, max_depth)
        consumed = r.pos
        error = None
    except (EOFError, ValueError) as exc:
        consumed = r.pos
        error = str(exc)
    return {
        "file": str(path),
        "size": len(data),
        "consumed": consumed,
        "error": error,
        "functions": out,
    }


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("files", nargs="+", type=Path)
    ap.add_argument("--json", type=Path)
    ap.add_argument("--depth", type=int, default=8)
    ap.add_argument("--quiet", action="store_true")
    args = ap.parse_args(argv)

    results = []
    for path in args.files:
        res = dump(path, args.depth)
        if res is None:
            print(f"{path.name}: not Lua 5.1 bytecode", file=sys.stderr)
            continue
        results.append(res)
        if not args.quiet:
            status = "OK" if not res["error"] else f"ERR {res['error']}"
            print(f"== {path.name} consumed={res['consumed']}/{res['size']} funcs={len(res['functions'])} {status}")
            for fn in res["functions"]:
                nums = ", ".join(f"{n:g}" for n in fn["numbers"][:20])
                strs = " | ".join(s for s in fn["strings"][:12])
                print(f"   d{fn['depth']} p{fn['params']} nums[{len(fn['numbers'])}]: {nums}")
                print(f"   d{fn['depth']} strs: {strs[:200]}")

    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(results, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"-> {args.json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
