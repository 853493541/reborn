"""Dump disassembly of specific VA ranges with string/IAT resolution.

Usage: python tools/dump_va.py FILE VA_START VA_END [VA_START VA_END ...]
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_MEM, X86_REG_RIP


def load(path: Path):
    pe = pefile.PE(str(path), fast_load=False)
    base = pe.OPTIONAL_HEADER.ImageBase
    secs = []
    for s in pe.sections:
        name = s.Name.rstrip(b"\x00").decode("ascii", "replace")
        secs.append((name, base + s.VirtualAddress, max(s.SizeOfRawData, s.Misc_VirtualSize), s.get_data()))
    imports = {}
    if hasattr(pe, "DIRECTORY_ENTRY_IMPORT"):
        for entry in pe.DIRECTORY_ENTRY_IMPORT:
            dll = entry.dll.decode("ascii", "replace")
            for imp in entry.imports:
                if imp.name:
                    imports[imp.address] = f"{dll}!{imp.name.decode('ascii', 'replace')}"
    return pe, base, secs, imports


def sec_for_va(secs, va):
    for name, base, size, data in secs:
        if base <= va < base + size:
            return name, base, data
    return None, None, None


def read_cstr(secs, va, limit=200):
    name, base, data = sec_for_va(secs, va)
    if data is None:
        return None
    off = va - base
    if off + 1 >= len(data):
        return None
    end = off
    while end < len(data) and data[end] != 0 and end - off < limit:
        end += 1
    raw = data[off:end]
    if not raw:
        return None
    printable = sum(1 for b in raw if 9 <= b <= 13 or 32 <= b <= 126)
    return raw.decode("ascii", "replace") if printable / len(raw) >= 0.9 else None


def main():
    path = Path(sys.argv[1])
    ranges = []
    args = sys.argv[2:]
    for i in range(0, len(args), 2):
        ranges.append((int(args[i], 0), int(args[i + 1], 0)))
    pe, base, secs, imports = load(path)
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    for start, end in ranges:
        name, sbase, data = sec_for_va(secs, start)
        print(f"\n==== {start:#x} .. {end:#x} ({name})")
        if data is None:
            print("  not mapped")
            continue
        code = data[start - sbase : end - sbase]
        for insn in md.disasm(code, start):
            text = f"  {insn.address:#x}: {insn.mnemonic:<8} {insn.op_str}"
            notes = []
            for op in insn.operands:
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                    t = insn.address + insn.size + op.mem.disp
                    s = read_cstr(secs, t)
                    imp = imports.get(t)
                    if s:
                        notes.append(f"\"{s[:70]}\"")
                    elif imp:
                        notes.append(imp)
                    else:
                        notes.append(f"{t:#x}")
            if notes:
                text += "  ; " + "; ".join(notes)
            print(text)


if __name__ == "__main__":
    main()
