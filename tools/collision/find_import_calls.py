#!/usr/bin/env python3
"""Find call sites of named imports (and direct calls to given exports) in a PE.

Usage: python tools/collision/find_import_calls.py <dll> [name ...]

Linear-disassembles executable sections, resolves `call/jmp qword ptr [rip+X]`
through the import table and direct calls into the export ranges, and prints
`rva  call/jmp  target-name` for every requested name (case-insensitive
substring match when the name is not an exact symbol).
"""
import re
import sys
from pathlib import Path

import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

RIP_MEM = re.compile(r"\[rip ([+-]) 0x([0-9a-f]+)\]")
IMAGE_SCN_CNT_CODE = 0x00000020
IMAGE_SCN_MEM_EXECUTE = 0x20000000


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    path = Path(sys.argv[1])
    wanted = [w.lower() for w in sys.argv[2:]]

    pe = pefile.PE(str(path), fast_load=True)
    pe.parse_data_directories(directories=[
        pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_IMPORT"],
        pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXPORT"],
    ])
    base = pe.OPTIONAL_HEADER.ImageBase

    names = {}
    for entry in getattr(pe, "DIRECTORY_ENTRY_IMPORT", []) or []:
        dll = entry.dll.decode("ascii", "replace")
        for imp in entry.imports:
            if imp.name:
                slot = imp.address if imp.address >= base else base + imp.address
                names[slot] = "imp:%s!%s" % (dll, imp.name.decode("ascii", "replace"))
    spans = []
    for exp in getattr(getattr(pe, "DIRECTORY_ENTRY_EXPORT", None), "symbols", []) or []:
        if exp.name:
            spans.append((base + exp.address, exp.name.decode("ascii", "replace")))

    def direct_name(target):
        for addr, name in spans:
            if target == addr:
                return "exp:" + name
        return None

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    hits = 0
    for sec in pe.sections:
        if not (sec.Characteristics & (IMAGE_SCN_CNT_CODE | IMAGE_SCN_MEM_EXECUTE)):
            continue
        code = sec.get_data()
        sec_va = base + sec.VirtualAddress
        for ins in md.disasm(code, sec_va):
            if ins.mnemonic not in ("call", "jmp"):
                continue
            target_name = None
            m = RIP_MEM.search(ins.op_str)
            if m:
                disp = int(m.group(2), 16)
                if m.group(1) == "-":
                    disp = -disp
                slot = ins.address + ins.size + disp
                target_name = names.get(slot)
            elif ins.op_str.startswith("0x"):
                target_name = direct_name(int(ins.op_str, 16))
            if not target_name:
                continue
            low = target_name.lower()
            if any(w in low for w in wanted):
                print("%08x  %-4s %s" % (ins.address - base, ins.mnemonic, target_name))
                hits += 1
    print("hits", hits)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
