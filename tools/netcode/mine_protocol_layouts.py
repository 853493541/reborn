#!/usr/bin/env python3
"""Mine S2C protocol field offsets from handler disassembly.

Reads the extracted protocol registration table (proof/netcode/protocol_table_s2c.tsv)
and, for every handler with a known VA, disassembles the handler prologue and collects
memory accesses of the form [reg + disp] where reg is a data register (not rsp/rbp/rip).
Those displacements are candidate packet-field offsets.

Read-only recon: never writes to the game install.
Usage:
  python tools/netcode/mine_protocol_layouts.py --table proof/netcode/protocol_table_s2c.tsv \
      --binary "...\\JX3LogicEditOperationX64.dll" --out proof/netcode/protocol_layouts_s2c.tsv
"""
from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_MEM, X86_OP_REG, X86_REG_RIP, X86_REG_RSP, X86_REG_RBP

STOP_MNEMONICS = {"ret", "jmp"}


def load(path: Path):
    pe = pefile.PE(str(path), fast_load=True)
    base = pe.OPTIONAL_HEADER.ImageBase
    secs = []
    for s in pe.sections:
        secs.append((s.VirtualAddress, s.SizeOfRawData, s.PointerToRawData))
    data = path.read_bytes()
    return base, secs, data


def va2off(secs, base, va):
    rva = va - base
    for vaddr, sraw, praw in secs:
        if vaddr <= rva < vaddr + sraw:
            return praw + (rva - vaddr)
    return None


def mine(binary: Path, table: Path, out: Path, window: int):
    base, secs, data = load(binary)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True

    rows = []
    with table.open("r", encoding="utf-8") as f:
        for line in f:
            if line.startswith("#") or not line.strip():
                continue
            parts = line.rstrip("\n").split("\t")
            if len(parts) < 5:
                continue
            rows.append(parts)

    total = 0
    with_fields = 0
    out_rows = []
    for parts in rows:
        sid, slot, handler, size_s, name = parts[0], parts[1], parts[2], parts[3], parts[4]
        try:
            handler_va = int(handler, 0)
        except ValueError:
            continue
        off = va2off(secs, base, handler_va)
        if off is None:
            continue
        try:
            declared = int(size_s, 0)
        except ValueError:
            declared = None
        code = data[off:off + window]
        packet_regs = {"rdx"}
        fields = []
        for ins in md.disasm(code, handler_va):
            if ins.mnemonic in STOP_MNEMONICS and ins.address != handler_va:
                break
            # collect packet-relative accesses
            for op in ins.operands:
                if op.type != X86_OP_MEM:
                    continue
                m = op.mem
                bname = ins.reg_name(m.base) if m.base else None
                if bname not in packet_regs:
                    continue
                if m.index:
                    continue
                disp = m.disp
                if disp < 0:
                    continue
                if declared is not None and disp >= declared:
                    continue
                if declared is None and disp > 0x400:
                    continue
                size = op.size if op.size else 0
                fields.append((disp, size))
            # update the packet-register set
            if ins.operands:
                dst = ins.operands[0]
                if dst.type == X86_OP_REG:
                    dname = ins.reg_name(dst.reg)
                    src = ins.operands[1] if len(ins.operands) > 1 else None
                    if ins.mnemonic == "mov" and src is not None and src.type == X86_OP_REG:
                        sname = ins.reg_name(src.reg)
                        if sname in packet_regs:
                            packet_regs.add(dname)
                        else:
                            packet_regs.discard(dname)
                    elif ins.mnemonic == "lea" and src is not None and src.type == X86_OP_MEM and src.mem.base and ins.reg_name(src.mem.base) in packet_regs and src.mem.disp >= 0:
                        # lea rX, [packet + disp] -> rX points into the packet
                        packet_regs.add(dname)
                    else:
                        packet_regs.discard(dname)
        total += 1
        if fields:
            with_fields += 1
        seen = set()
        uniq = []
        for d, s in sorted(fields):
            k = (d, s)
            if k in seen:
                continue
            seen.add(k)
            uniq.append("%d:%d" % (d, s))
        out_rows.append([sid, size_s, name, ";".join(uniq[:64])])

    with out.open("w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, delimiter="\t")
        w.writerow(["id", "size", "name", "fields(off:bytes)"])
        w.writerows(out_rows)
    print("handlers processed: %d, with >=1 candidate field: %d, rows written: %d" % (total, with_fields, len(out_rows)))


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--table", type=Path, default=Path("proof/netcode/protocol_table_s2c.tsv"))
    ap.add_argument("--binary", type=Path, required=True)
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--window", type=int, default=320)
    args = ap.parse_args(argv)
    mine(args.binary, args.table, args.out, args.window)


if __name__ == "__main__":
    main()
