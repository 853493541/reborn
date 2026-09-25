#!/usr/bin/env python3
"""Disassemble around a named string xref (function identification helper).

Read-only static research helper for the reborn JX3 netcode work.

Usage:
  python disasm_fn.py <binary> <substring> [--window 0x400] [--before 0x40]
                      [--maxhits 1] [--walkback]
"""
from __future__ import annotations

import argparse
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs

IMAGE_SCN_CNT_CODE = 0x00000020
IMAGE_SCN_MEM_EXECUTE = 0x20000000


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("binary")
    ap.add_argument("needle")
    ap.add_argument("--window", type=lambda v: int(v, 0), default=0x400)
    ap.add_argument("--before", type=lambda v: int(v, 0), default=0x40)
    ap.add_argument("--maxhits", type=int, default=1)
    ap.add_argument("--walkback", action="store_true",
                    help="walk 0xCC padding backwards from first xref to function start")
    args = ap.parse_args()

    pe = pefile.PE(args.binary, fast_load=False)
    base = pe.OPTIONAL_HEADER.ImageBase
    data = pe.get_memory_mapped_image()

    # locate string (bytes, GBK-safe ascii)
    needle = args.needle.encode("latin-1", "replace")
    str_vas = []
    pos = 0
    while len(str_vas) < 64:
        pos = data.find(needle, pos)
        if pos < 0:
            break
        # only accept if it starts a null-terminated string (preceded by NUL)
        if pos == 0 or data[pos - 1] == 0:
            str_vas.append(base + pos)
        pos += 1
    if not str_vas:
        print(f"string not found: {args.needle}")
        return 1
    print(f"string VAs: {[hex(v) for v in str_vas]}")

    # find xrefs (lea/mov rip-relative) in executable sections
    text = []
    for s in pe.sections:
        if s.Characteristics & (IMAGE_SCN_CNT_CODE | IMAGE_SCN_MEM_EXECUTE):
            text.append((base + s.VirtualAddress, s.get_data()))

    xrefs = []
    for sva, sdata in text:
        for va in str_vas:
            target = va - sva          # this is the disp if base of instr... easier below
        # scan for e8/e9? just scan 4-byte little-endian disp occurrences is noisy;
        # do a proper linear disasm for rip-relative references
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    md.skipdata = True
    for sva, sdata in text:
        for insn in md.disasm(sdata, sva):
            if not insn.id:
                continue
            try:
                for op in insn.operands:
                    if op.type == 3 and op.mem.base == 41:  # X86_OP_MEM, RIP
                        tgt = insn.address + insn.size + op.mem.disp
                        if tgt in str_vas:
                            xrefs.append((insn.address, tgt, insn))
            except Exception:
                pass
    xrefs.sort()
    print(f"xrefs: {len(xrefs)}")
    for addr, tgt, insn in xrefs[:args.maxhits]:
        print(f"\n=== xref @ {addr:#x} -> {tgt:#x} : {insn.mnemonic} {insn.op_str}")
        start = addr
        if args.walkback:
            # find function start: walk backwards for 0xCC CC run, cap 0x10000
            off = addr - base
            i = off - 1
            limit = max(0, off - 0x10000)
            while i > limit:
                if data[i] == 0xCC and data[i - 1] == 0xCC:
                    start = base + i + 1
                    break
                i -= 1
            print(f"function start (0xCC walk): {start:#x}")
        lo = max(start, addr - args.before)
        # disassemble from function start or lo
        sva = None
        sdata = None
        for sva2, sdata2 in text:
            if sva2 <= lo < sva2 + len(sdata2):
                sva, sdata = sva2, sdata2
                break
        if sva is None:
            continue
        off = lo - sva
        count = 0
        for insn in md.disasm(sdata[off:off + args.window + (addr - lo)], lo):
            marker = " <<<" if insn.address == addr else ""
            print(f"{insn.address:#012x}: {insn.mnemonic}\t{insn.op_str}{marker}")
            count += 1
            if count > 400:
                break
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
