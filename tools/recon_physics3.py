"""Third-pass physics recon: manager vtable, full-prefix string xrefs, imports.

Usage: python tools/recon_physics3.py
"""
from __future__ import annotations

import struct
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

PE = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PhysicsEngineX64.dll")
EDITOR_PE = Path(r"C:\SeasunGame\MovieEditor\bin64\PhysicsEngineX64.dll")
PHYSICSX = Path(r"C:\SeasunGame\MovieEditor\bin64\PhysicsX64.dll")
ADAPTER = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KG3DEngineAdapterX64.dll")

MANAGER_VTABLE = 0x18000F860

KEY_STRINGS = [
    "PhysicsEngine::PhysicsManager::_InitPhysX",
    "PhysicsEngine::PhysicsManager::CreatePhysicsScene",
    "PhysicsEngine::PhysicsManager::CreatePhysXTerrain",
    "PhysicsEngine::PhysicsManager::CreatePhysicsSceneLoader",
    "PhysicsEngine::PhysicsManager::SetWorkingDir",
    "PhysicsEngine::KG3D_PhysxTerrain::LoadTerrain",
    "PhysicsEngine::KG3D_PhysxTerrain::GetTileBBox",
    "PhysicsEngine::KG3D_PhysxTerrain::CreateEditInterface",
    "PhysicsEngine::PhysicsScene::SweepEx",
    "PhysicsEngine::PhysicsScene::RayCast",
    "PhysicsEngine::PhysicsScene::GetFloorHeight",
    "PhysicsEngine::QueryPhysXParam",
]


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
    if printable / len(raw) < 0.9:
        return None
    return raw.decode("ascii", "replace")


def read_u64(secs, va):
    name, base, data = sec_for_va(secs, va)
    if data is None:
        return None
    off = va - base
    if off < 0 or off + 8 > len(data):
        return None
    return struct.unpack_from("<Q", data, off)[0]


def disasm_func(md, secs, va, max_insn=45):
    name, base, data = sec_for_va(secs, va)
    if data is None or not name.lower().startswith(".text"):
        return [f"    (not code: {va:#x})"]
    lines = []
    end_va = va - base + 4096
    for insn in md.disasm(data[va - base : end_va], va):
        text = f"    {insn.address:#x}: {insn.mnemonic:<8} {insn.op_str}"
        for op in insn.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                target = insn.address + insn.size + op.mem.disp
                s = read_cstr(secs, target)
                if s:
                    text += f"  ; \"{s[:70]}\""
                else:
                    imp = None
                    text += f"  ; -> {target:#x}"
        lines.append(text)
        if insn.mnemonic in ("ret",) and len(lines) > 2:
            break
        if len(lines) >= max_insn:
            lines.append("    ...")
            break
    return lines


def find_string_vas(secs, needle):
    hits = []
    data = needle.encode("ascii")
    for name, base, size, sdata in secs:
        start = 0
        while True:
            i = sdata.find(data, start)
            if i < 0:
                break
            if i + len(data) < len(sdata) and sdata[i + len(data)] == 0:
                hits.append(base + i)
            start = i + 1
    return hits


def find_xrefs(md, secs, targets):
    refs = []
    for name, base, size, data in secs:
        if not name.lower().startswith(".text"):
            continue
        for insn in md.disasm(data, base):
            for op in insn.operands:
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                    t = insn.address + insn.size + op.mem.disp
                    if t in targets:
                        refs.append((insn.address, t))
                elif op.type == X86_OP_IMM and op.imm in targets:
                    refs.append((insn.address, op.imm))
    return refs


def imports(pe):
    out = {}
    if hasattr(pe, "DIRECTORY_ENTRY_IMPORT"):
        for entry in pe.DIRECTORY_ENTRY_IMPORT:
            out[entry.dll.decode("ascii", "replace")] = len(entry.imports)
    return out


def main():
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True

    pe, base, secs = load(PE)

    print("=" * 100)
    print(f"IMPORTS {PE.name}: " + ", ".join(f"{k}({v})" for k, v in sorted(imports(pe).items())))
    if EDITOR_PE.is_file():
        pe_e, _, _ = load(EDITOR_PE)
        print(f"IMPORTS {EDITOR_PE.name}: " + ", ".join(f"{k}({v})" for k, v in sorted(imports(pe_e).items())))
    if PHYSICSX.is_file():
        pe_x, _, _ = load(PHYSICSX)
        print(f"IMPORTS {PHYSICSX.name}: " + ", ".join(f"{k}({v})" for k, v in sorted(imports(pe_x).items())))

    print("\n" + "=" * 100)
    print(f"MANAGER vtable @ {MANAGER_VTABLE:#x}")
    for i in range(24):
        fn = read_u64(secs, MANAGER_VTABLE + 8 * i)
        if not fn:
            print(f"  [{i:2}] 0")
            continue
        print(f"  [{i:2}] {fn:#x}")
        for line in disasm_func(md, secs, fn, max_insn=10):
            print("      " + line.strip())

    print("\n" + "=" * 100)
    print("STRING XREFS")
    for needle in KEY_STRINGS:
        vas = find_string_vas(secs, needle)
        if not vas:
            print(f"\n-- \"{needle}\" NOT FOUND")
            continue
        for sva in vas:
            refs = find_xrefs(md, secs, {sva})
            print(f"\n-- \"{needle}\" @ {sva:#x} xrefs={len(refs)}")
            for insn_va, _ in refs[:2]:
                print(f"   from {insn_va:#x}")
                root = insn_va
                for line in disasm_func(md, secs, root - 160, max_insn=70):
                    print("   " + line)

    print("\n" + "=" * 100)
    print(f"ADAPTER imports of interest: {ADAPTER.name}")
    pe_a, base_a, secs_a = load(ADAPTER)
    imp_a = imports(pe_a)
    print("  " + ", ".join(f"{k}({v})" for k, v in sorted(imp_a.items())))
    # absolute pointer table scan: find 8-byte slots holding the two string VAs
    for needle in ("PhysicsEngineX64.dll", "GetPhysicsManager"):
        for sva in find_string_vas(secs_a, needle):
            print(f"\n  string \"{needle}\" @ {sva:#x}")
            for name, sbase, size, data in secs_a:
                pat = struct.pack("<Q", sva)
                start = 0
                while True:
                    i = data.find(pat, start)
                    if i < 0:
                        break
                    slot_va = sbase + i
                    print(f"    ptr slot @ {slot_va:#x} (section {name})")
                    back = find_xrefs(md, secs_a, {slot_va})
                    for insn_va, _ in back[:2]:
                        print(f"      xref from {insn_va:#x}")
                        for line in disasm_func(md, secs_a, insn_va - 48, max_insn=30):
                            print("      " + line)
                    start = i + 8


if __name__ == "__main__":
    main()
