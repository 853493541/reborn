#!/usr/bin/env python3
"""Find code references to struct-field displacements in a PE.

Usage: python tools/collision/find_field_refs.py <dll> <disp-hex> [disp-hex ...]

Disassembles all executable sections and prints instructions whose memory
operand displacement equals one of the given values. Useful for locating the
consumers of known struct members (e.g. the physic-list sets).
"""
import sys
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64
from capstone.x86 import X86_OP_MEM

IMAGE_SCN_CNT_CODE = 0x00000020
IMAGE_SCN_MEM_EXECUTE = 0x20000000


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    path = Path(sys.argv[1])
    disps = {int(v, 16) for v in sys.argv[2:]}
    pe = pefile.PE(str(path))
    base = pe.OPTIONAL_HEADER.ImageBase
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    md.skipdata = True
    total = 0
    for sec in pe.sections:
        if not (sec.Characteristics & (IMAGE_SCN_CNT_CODE | IMAGE_SCN_MEM_EXECUTE)):
            continue
        for ins in md.disasm(sec.get_data(), base + sec.VirtualAddress):
            if ins.id == 0:  # skipdata pseudo-instruction
                continue
            for op in ins.operands:
                if op.type == X86_OP_MEM and op.mem.disp in disps:
                    print("%x  +0x%x  %s %s" % (ins.address, op.mem.disp, ins.mnemonic, ins.op_str))
                    total += 1
    print("refs", total)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
