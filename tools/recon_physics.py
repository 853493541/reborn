"""Disassemble PhysicsEngineX64.dll exports and their call sites in the game adapter.

Purpose: derive callable signatures for the five C exports
(GetPhysicsManager, CreatePhysicsTerrainDataLoader, CreatePhysicsTerrain,
CreatePhysicsSceneDynamicLoader, CreateSceneFileDataLoader) and find how
KG3DEngineAdapterX64.dll invokes them.

Usage: python tools/recon_physics.py [--dll PATH] [--adapter PATH]
"""
from __future__ import annotations

import argparse
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_MEM, X86_OP_REG, X86_REG_RIP

DEFAULT_DLL = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PhysicsEngineX64.dll")
DEFAULT_ADAPTER = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KG3DEngineAdapterX64.dll")

EXPORTS = [
    "GetPhysicsManager",
    "CreatePhysicsTerrainDataLoader",
    "CreatePhysicsTerrain",
    "CreatePhysicsSceneDynamicLoader",
    "CreateSceneFileDataLoader",
]

STRINGS_OF_INTEREST = [
    "GetPhysicsManager",
    "PhysicsEngineX64.dll",
    "PhysicsX64.dll",
    "CreatePhysicsTerrainDataLoader",
    "CreatePhysicsTerrain",
    "CreatePhysicsSceneDynamicLoader",
    "CreateSceneFileDataLoader",
]


def load(path: Path):
    pe = pefile.PE(str(path), fast_load=False)
    image_base = pe.OPTIONAL_HEADER.ImageBase
    sections = []
    for s in pe.sections:
        name = s.Name.rstrip(b"\x00").decode("ascii", "replace")
        sections.append((name, image_base + s.VirtualAddress, s.SizeOfRawData, s.get_data()))
    return pe, image_base, sections


def find_sections_for_va(sections, va):
    for name, base, size, data in sections:
        if base <= va < base + max(size, 1):
            return name, base, data
    return None, None, None


def read_cstring(sections, va, limit=256):
    name, base, data = find_sections_for_va(sections, va)
    if data is None:
        return None
    off = va - base
    end = off
    while end < len(data) and data[end] != 0 and end - off < limit:
        end += 1
    raw = data[off:end]
    if not raw:
        return None
    return raw.decode("ascii", "replace")


def va_to_raw_offset(pe, va):
    rva = va - pe.OPTIONAL_HEADER.ImageBase
    for s in pe.sections:
        if s.VirtualAddress <= rva < s.VirtualAddress + max(s.SizeOfRawData, s.Misc_VirtualSize):
            return s.PointerToRawData + (rva - s.VirtualAddress)
    return None


def iat_names(pe):
    names = {}
    if not hasattr(pe, "DIRECTORY_ENTRY_IMPORT"):
        return names
    for entry in pe.DIRECTORY_ENTRY_IMPORT:
        for imp in entry.imports:
            if imp.name:
                names[imp.address] = imp.name.decode("ascii", "replace")
    return names


def exports_by_name(pe):
    out = {}
    if not hasattr(pe, "DIRECTORY_ENTRY_EXPORT"):
        return out
    for exp in pe.DIRECTORY_ENTRY_EXPORT.symbols:
        if exp.name:
            out[exp.name.decode("ascii", "replace")] = pe.OPTIONAL_HEADER.ImageBase + exp.address
    return out


def disasm_func(md: Cs, sections, pe, va, imports, max_insn=260):
    name, base, data = find_sections_for_va(sections, va)
    if data is None:
        print(f"    (va {va:#x} not in any section)")
        return
    code = data[va - base : va - base + 4096]
    lines = []
    for insn in md.disasm(code, va):
        text = f"  {insn.address:#010x}: {insn.mnemonic:<8} {insn.op_str}"
        # resolve RIP-relative references
        for op in insn.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                target = insn.address + insn.size + op.mem.disp
                s = read_cstring(sections, target)
                if s:
                    text += f"  ; -> \"{s[:80]}\""
                else:
                    imp = imports.get(target)
                    if imp:
                        text += f"  ; -> import {imp}"
                    else:
                        text += f"  ; -> {target:#x}"
        lines.append(text)
        if insn.mnemonic == "ret" and len(lines) > 3:
            break
        if len(lines) >= max_insn:
            lines.append("  ... (truncated)")
            break
    print("\n".join(lines))


def find_string_vas(sections, needle: str):
    hits = []
    data = needle.encode("ascii")
    for name, base, size, sdata in sections:
        start = 0
        while True:
            i = sdata.find(data, start)
            if i < 0:
                break
            # require NUL terminator for rdata strings
            if i + len(data) < len(sdata) and sdata[i + len(data)] == 0:
                hits.append(base + i)
            start = i + 1
    return hits


def find_xrefs(md: Cs, sections, targets: set[int]):
    """Linear-scan .text for RIP-relative lea/mov referencing target VAs."""
    refs = []
    for name, base, size, data in sections:
        if name.lower() != ".text":
            continue
        for insn in md.disasm(data, base):
            for op in insn.operands:
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                    target = insn.address + insn.size + op.mem.disp
                    if target in targets:
                        refs.append((insn.address, target))
    return refs


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dll", type=Path, default=DEFAULT_DLL)
    ap.add_argument("--adapter", type=Path, default=DEFAULT_ADAPTER)
    args = ap.parse_args()

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True

    print("#" * 90)
    print(f"# EXPORTS in {args.dll}")
    print("#" * 90)
    pe, image_base, sections = load(args.dll)
    exp = exports_by_name(pe)
    imports = iat_names(pe)
    for name in EXPORTS:
        va = exp.get(name)
        print(f"\n==== {name} @ {va:#x}" if va else f"\n==== {name} (not found)")
        if va:
            disasm_func(md, sections, pe, va, imports)

    if not args.adapter or not args.adapter.is_file():
        return
    print("\n" + "#" * 90)
    print(f"# XREFS in {args.adapter}")
    print("#" * 90)
    pe2, base2, sections2 = load(args.adapter)
    target_vas = {}
    for needle in STRINGS_OF_INTEREST:
        for va in find_string_vas(sections2, needle):
            target_vas.setdefault(va, needle)
    if not target_vas:
        print("  no string targets found")
        return
    for va, needle in sorted(target_vas.items()):
        print(f"\n-- string \"{needle}\" @ {va:#x}")
    refs = find_xrefs(md, sections2, set(target_vas))
    print(f"\n{len(refs)} xrefs")
    seen_funcs = set()
    for insn_va, target in refs:
        key = (target, insn_va >> 8)
        if key in seen_funcs:
            continue
        seen_funcs.add(key)
        print(f"\n---- xref to \"{target_vas[target]}\" from {insn_va:#x} (context)")
        disasm_func(md, sections2, pe2, insn_va - 64, {}, max_insn=40)


if __name__ == "__main__":
    main()
