"""Extract packet-field maps for game S2C handlers.

For each handler, disassemble and pair `mov reg, [pkt_reg + src]` (packet reads)
with the following `mov [dest_reg + dst], reg` (object writes) by tracking the
loaded register. Emits a TSV: handler_va, src_offset, dest_field, read_va, write_va.

Usage:
  python extract_field_maps.py --exe <path> [--handler 0xVA:size ...]
  python extract_field_maps.py --exe <path> --out <tsv>
"""
import argparse
import struct

import capstone
import pefile


def load(exe):
    pe = pefile.PE(exe, fast_load=True)
    return pe, pe.OPTIONAL_HEADER.ImageBase


def func_bytes(pe, base, va, size):
    return pe.get_data(va - base, size)


def parse_operand(ins, op_index):
    """Return (base_reg, disp, size) for a memory operand, or None."""
    ops = ins.operands
    if op_index >= len(ops):
        return None
    op = ops[op_index]
    if op.type != capstone.x86.X86_OP_MEM:
        return None
    mem = op.mem
    base = ins.reg_name(mem.base) if mem.base else None
    return (base, mem.disp, op.size)


def extract(exe, handler, size, pkt_regs=("rdi", "rsi", "rbx", "rbp", "r8", "r9", "rdx", "rcx",
                                          "r14", "r15", "r12", "r13")):
    pe, base = load(exe)
    md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
    md.detail = True
    raw = func_bytes(pe, base, handler, size)
    rows = []
    pending = {}  # reg -> (src_off, read_va)
    for ins in md.disasm(raw, handler):
        try:
            if ins.mnemonic in ("mov", "movzx", "movsx") and len(ins.operands) == 2:
                dst, src = ins.operands
                # packet read: mov reg, [pkt_reg + disp]
                if dst.type == capstone.x86.X86_OP_REG and src.type == capstone.x86.X86_OP_MEM:
                    sbase = ins.reg_name(src.mem.base) if src.mem.base else None
                    if sbase in pkt_regs:
                        pending[ins.reg_name(dst.reg)] = (src.mem.disp, ins.address)
                # object write: mov [dst_reg + disp], reg
                elif dst.type == capstone.x86.X86_OP_MEM and src.type == capstone.x86.X86_OP_REG:
                    sreg = ins.reg_name(src.reg)
                    if sreg in pending:
                        src_off, read_va = pending[sreg]
                        dbase = ins.reg_name(dst.mem.base) if dst.mem.base else None
                        rows.append((handler, src_off, dbase, dst.mem.disp, read_va, ins.address))
        except capstone.CsError:
            continue
    return rows


DEFAULT_HANDLERS = [
    (0x14015C1D0, 2435, "id4 OnSyncPlayerBaseInfo"),
    (0x14015A120, 3637, "id10 OnSyncNewPlayer"),
    (0x1401597A0, 2021, "id11 OnSyncNewNpc"),
    (0x1401591D0, 1475, "id12 OnSyncNewDoodad"),
    (0x1401460F0, 497, "id13 OnMoveCharacter"),
    (0x14014CEB0, 691, "id8 OnSwitchMap"),
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe", required=True)
    ap.add_argument("--out", default=None)
    args = ap.parse_args()
    lines = ["handler_va\tsrc_offset\tdest_base\tdest_offset\tread_va\twrite_va\tname"]
    for va, size, name in DEFAULT_HANDLERS:
        for row in extract(args.exe, va, size):
            lines.append("0x%X\t0x%X\t%s\t0x%X\t0x%X\t0x%X\t%s"
                         % (row[0], row[1], row[2], row[3], row[4], row[5], name))
    text = "\n".join(lines) + "\n"
    if args.out:
        open(args.out, "w", encoding="utf-8").write(text)
        print("wrote %s (%d rows)" % (args.out, len(lines) - 1))
    else:
        print(text)


if __name__ == "__main__":
    main()
