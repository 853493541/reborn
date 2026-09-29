#!/usr/bin/env python3
"""Disassemble an RVA range of a PE and annotate calls/constants.

Usage: python tools/collision/disasm_range.py <exe-or-dll> <start-rva-hex> <size-hex> <out.txt>

Annotations:
  ; imp: DLL!name        call/jmp through the IAT slot
  ; exp: name            direct call/jmp to an export start
  ; =<float>             RIP-relative float constant used by SSE ops
  ; =<u32>/<qword>       integer constant read
"""
import re
import struct
import sys
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

RIP_MEM = re.compile(r"\[rip [+-] 0x([0-9a-f]+)\]")


def main() -> int:
    if len(sys.argv) != 5:
        print(__doc__)
        return 2
    path = Path(sys.argv[1])
    start = int(sys.argv[2], 16)
    size = int(sys.argv[3], 16)
    out = Path(sys.argv[4])

    pe = pefile.PE(str(path), fast_load=True)
    pe.parse_data_directories(directories=[
        pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"],
        pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXPORT"],
    ])
    base = pe.OPTIONAL_HEADER.ImageBase

    names = {}
    for entry in getattr(pe, "DIRECTORY_ENTRY_IMPORT", []) or []:
        for imp in entry.imports:
            if imp.name:
                slot = imp.address if imp.address >= base else base + imp.address
                names[slot] = "imp:%s!%s" % (
                    entry.dll.decode("ascii", "replace"),
                    imp.name.decode("ascii", "replace"))
    for exp in getattr(getattr(pe, "DIRECTORY_ENTRY_EXPORT", None), "symbols", []) or []:
        if exp.name:
            names[base + exp.address] = "exp:" + exp.name.decode("ascii", "replace")

    image = pe.get_memory_mapped_image()

    def read_at(rva, n):
        if 0 <= rva < len(image) - n:
            return image[rva:rva + n]
        return b""

    data = image[start:start + size]
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    lines = []
    for ins in md.disasm(data, base + start):
        note = ""
        m = RIP_MEM.search(ins.op_str)
        if m:
            disp = int(m.group(1), 16)
            if "[rip -" in ins.op_str:
                disp = -disp
            target = ins.address + ins.size + disp
            if target in names and ins.mnemonic in ("call", "jmp"):
                note = "  ; " + names[target]
            elif ins.mnemonic in ("movss", "mulss", "addss", "subss", "divss",
                                  "movaps", "movups", "movsd", "comiss", "ucomiss"):
                raw = read_at(target - base, 4 if ins.mnemonic != "movsd" else 8)
                if len(raw) == 4:
                    note = "  ; =%.6g" % struct.unpack("<f", raw)[0]
                elif len(raw) == 8:
                    note = "  ; =%.6g (f64)" % struct.unpack("<d", raw)[0]
        raw = ins.bytes.hex()
        lines.append("%08x  %-22s %-7s %s%s" % (
            ins.address - base, raw, ins.mnemonic, ins.op_str, note))
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("%s rva=%x size=%x -> %s (%d insns)" % (path.name, start, size, out, len(lines)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
