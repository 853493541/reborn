"""Build a function map for PhysicsEngineX64.dll.

Linear sweep -> call targets -> function boundaries; name functions by the
"PhysicsEngine::..." / "KG3D_..." strings they embed (KGLOG macros).
Then annotate the vtables of interest.

Usage: python tools/recon_physics_funcs.py
"""
from __future__ import annotations

import struct
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

PE = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PhysicsEngineX64.dll")
VTABLES = {
    "PhysicsManager": 0x1800FA6B0,
    "PhysicsTerrain": 0x1800FCFE0,
    "PhysicsTerrainDataLoader": 0x1800FD2B0,
}


def load(path: Path):
    pe = pefile.PE(str(path), fast_load=False)
    base = pe.OPTIONAL_HEADER.ImageBase
    secs = []
    for s in pe.sections:
        name = s.Name.rstrip(b"\x00").decode("ascii", "replace")
        secs.append((name, base + s.VirtualAddress, max(s.SizeOfRawData, s.Misc_VirtualSize), s.get_data()))
    return pe, base, secs


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


def read_u64(secs, va):
    name, base, data = sec_for_va(secs, va)
    if data is None:
        return None
    off = va - base
    if off < 0 or off + 8 > len(data):
        return None
    return struct.unpack_from("<Q", data, off)[0]


def main():
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    pe, base, secs = load(PE)

    text = None
    for name, sbase, size, data in secs:
        if name.lower().startswith(".text"):
            text = (sbase, data)
            break
    assert text
    tbase, tdata = text

    insns = {}
    call_targets = set()
    string_refs = []  # (insn_va, target_va, string)
    for insn in md.disasm(tdata, tbase):
        insns[insn.address] = insn
        for op in insn.operands:
            if op.type == X86_OP_IMM and insn.mnemonic == "call":
                call_targets.add(op.imm)
            elif op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                t = insn.address + insn.size + op.mem.disp
                s = read_cstr(secs, t)
                if s:
                    string_refs.append((insn.address, t, s))

    exports = {}
    if hasattr(pe, "DIRECTORY_ENTRY_EXPORT"):
        for e in pe.DIRECTORY_ENTRY_EXPORT.symbols:
            if e.name:
                exports[pe.OPTIONAL_HEADER.ImageBase + e.address] = e.name.decode("ascii", "replace")

    vtable_entries = {}
    for label, va in VTABLES.items():
        for i in range(40):
            fn = read_u64(secs, va + 8 * i)
            if fn:
                vtable_entries[(label, i)] = fn
            else:
                vtable_entries[(label, i)] = 0

    starts = set(exports) | set(call_targets)
    for fn in vtable_entries.values():
        if fn:
            starts.add(fn)
    starts = {s for s in starts if s in insns or (tbase <= s < tbase + len(tdata))}

    sorted_starts = sorted(starts)
    # name each function: first "::" string ref encountered inside its span
    names = {}
    for idx, s in enumerate(sorted_starts):
        end = sorted_starts[idx + 1] if idx + 1 < len(sorted_starts) else s + 0x2000
        for rva, tva, stext in string_refs:
            if s <= rva < end and "::" in stext and not stext.startswith("KGLOG"):
                names.setdefault(s, stext)
    # fallback: any string ref in range
    for idx, s in enumerate(sorted_starts):
        if s in names:
            continue
        end = sorted_starts[idx + 1] if idx + 1 < len(sorted_starts) else s + 0x2000
        for rva, tva, stext in string_refs:
            if s <= rva < end:
                names.setdefault(s, stext.split("(")[0].strip())
                break

    print(f"# functions: {len(sorted_starts)}, named: {len(names)}")
    print("\n=== named functions ===")
    for s in sorted_starts:
        nm = names.get(s)
        if nm:
            print(f"  {s:#x}  {nm}")

    print("\n=== vtables ===")
    for label, _ in VTABLES.items():
        print(f"\n-- {label}")
        for i in range(40):
            fn = vtable_entries[(label, i)]
            if not fn:
                print(f"  [{i:2}] 0")
                continue
            nm = names.get(fn, "?")
            ex = exports.get(fn, "")
            print(f"  [{i:2}] {fn:#x}  {nm}{'  (export: ' + ex + ')' if ex else ''}")


if __name__ == "__main__":
    main()
