#!/usr/bin/env python3
"""Find RIP-relative code references to arbitrary VAs and disassemble around them.

Variant of xref_string.py for data addresses (globals, vtables) instead of
strings. Used to trace where a global object is initialized.

Usage:
  python tools/netcode/xref_va.py BINARY 0x180EDDFE0 --out out.txt
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


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("binary", type=Path)
    ap.add_argument("targets", nargs="+", help="hex VAs, e.g. 0x180EDDFE0")
    ap.add_argument("--out", type=Path)
    ap.add_argument("--after", type=int, default=120)
    args = ap.parse_args(argv)

    targets = {int(t, 0) for t in args.targets}
    pe = pefile.PE(str(args.binary), fast_load=False)
    base = pe.OPTIONAL_HEADER.ImageBase
    secs = []
    for s in pe.sections:
        name = s.Name.rstrip(b"\x00").decode("ascii", "replace")
        secs.append({
            "name": name, "va": base + s.VirtualAddress, "data": s.get_data(),
            "code": bool(s.Characteristics & (IMAGE_SCN_CNT_CODE | IMAGE_SCN_MEM_EXECUTE)),
        })

    def sec_for(va):
        for s in secs:
            if s["va"] <= va < s["va"] + len(s["data"]) + 0x1000:
                return s
        return None

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    md.skipdata = True

    lines = [f"# {args.binary.name} xrefs to {', '.join(hex(t) for t in sorted(targets))}"]
    found = []
    for sec in secs:
        if not sec["code"] or not sec["data"]:
            continue
        for insn in md.disasm(sec["data"], sec["va"]):
            if insn.id == 0:
                continue
            for op in insn.operands:
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                    t = insn.address + insn.size + op.mem.disp
                    if t in targets:
                        found.append((insn.address, t))
                elif op.type == X86_OP_IMM and op.imm in targets:
                    found.append((insn.address, op.imm))

    seen = set()
    for addr, t in sorted(found):
        if addr in seen:
            continue
        seen.add(addr)
        start = max(sec_for(addr)["va"], addr - 60)
        end = addr + args.after
        sec = sec_for(addr)
        lines.append(f"\n## ref @ {addr:#x} -> {t:#x}")
        for insn in md.disasm(sec["data"][start - sec["va"]: end - sec["va"]], start):
            lines.append(f"  {insn.address:#x}: {insn.mnemonic:<8} {insn.op_str}")

    text = "\n".join(lines) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8", errors="replace")
        print(f"{len(found)} refs -> {args.out}")
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
