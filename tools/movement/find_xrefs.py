#!/usr/bin/env python3
"""Find direct call sites and RIP-relative field accesses to an address in a PE.

Read-only helper for the JX3 movement research.

Usage:
  # callers of a function VA (E8 rel32)
  python tools/movement/find_xrefs.py BINARY --call 0x180857df0

  # accesses to a global struct field (RIP-relative), base..base+size
  python tools/movement/find_xrefs.py BINARY --field 0x1805b0600 --size 0x100

Prints address, kind and disassembly line with the resolved target.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_MEM, X86_OP_IMM, X86_REG_RIP

IMAGE_SCN_CNT_CODE = 0x00000020
IMAGE_SCN_MEM_EXECUTE = 0x20000000


def load(path: Path):
    pe = pefile.PE(str(path), fast_load=False)
    base = pe.OPTIONAL_HEADER.ImageBase
    sections = []
    for s in pe.sections:
        sections.append(
            {
                "name": s.Name.rstrip(b"\x00").decode("ascii", "replace"),
                "va": base + s.VirtualAddress,
                "vsize": max(s.Misc_VirtualSize, s.SizeOfRawData),
                "data": s.get_data(),
                "code": bool(s.Characteristics & (IMAGE_SCN_CNT_CODE | IMAGE_SCN_MEM_EXECUTE)),
            }
        )
    return pe, base, sections


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("binary", type=Path)
    ap.add_argument("--call", type=lambda v: int(v, 0), default=None)
    ap.add_argument("--field", type=lambda v: int(v, 0), default=None)
    ap.add_argument("--size", type=lambda v: int(v, 0), default=0x400)
    ap.add_argument("--around", type=lambda v: int(v, 0), default=None,
                    help="disassemble a window around this VA")
    ap.add_argument("--before", type=lambda v: int(v, 0), default=0x60)
    ap.add_argument("--after", type=lambda v: int(v, 0), default=0x100)
    args = ap.parse_args(argv)

    if args.call is None and args.field is None and args.around is None:
        ap.error("need --call, --field or --around")

    pe, base, sections = load(args.binary)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    md.skipdata = True

    code_secs = [s for s in sections if s["code"] and s["data"]]
    lo = args.field if args.field is not None else 0
    hi = lo + args.size

    if args.around is not None:
        sec = next((s for s in code_secs if s["va"] <= args.around < s["va"] + s["vsize"]), None)
        if sec is None:
            print("address not in a code section", file=sys.stderr)
            return 2
        start = args.around - args.before
        off = start - sec["va"]
        code = sec["data"][off: off + args.before + args.after]
        for insn in md.disasm(code, start):
            print(f"{insn.address:#x}: {insn.mnemonic:<8} {insn.op_str}")
        return 0

    for sec in code_secs:
        for insn in md.disasm(sec["data"], sec["va"]):
            if insn.id == 0:
                continue
            if args.call is not None:
                if insn.mnemonic == "call" and insn.operands:
                    op = insn.operands[0]
                    if op.type == X86_OP_IMM and op.imm == args.call:
                        print(f"{insn.address:#x}: call {args.call:#x}")
                    elif op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                        t = insn.address + insn.size + op.mem.disp
                        if t == args.call:
                            print(f"{insn.address:#x}: call [rip -> {t:#x}]")
            if args.field is not None:
                for op in insn.operands:
                    if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                        t = insn.address + insn.size + op.mem.disp
                        if lo <= t < hi:
                            off = t - lo
                            print(f"{insn.address:#x}: {insn.mnemonic} {insn.op_str}  ; field+{off:#x}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
