"""Second-pass physics recon: vtables, function-name xrefs, adapter call sites.

- Dumps the vtables of objects created by the PhysicsEngineX64 factories.
- Locates internal functions by their KLG string references (e.g. LoadTerrain).
- Finds code references (rip-relative or absolute imm) to the loader strings
  in KG3DEngineAdapterX64.dll, including non-.text sections.

Usage: python tools/recon_physics2.py
"""
from __future__ import annotations

import struct
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_REG_RIP

PE = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PhysicsEngineX64.dll")
ADAPTER = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\KG3DEngineAdapterX64.dll")

# vtable addresses observed in factory disassembly (image base 0x180000000)
VTABLES = {
    "PhysicsTerrain": 0x1800FCFE0,
    "PhysicsSceneDynamicLoader": 0x1800FBC98,
    "PhysicsTerrainDataLoader": 0x1800FD2B0,
}
SINGLETON = 0x18011B5D0
ALLOC_GLOBAL = 0x18011F460
FS_GLOBAL = 0x18011F4F8

KEY_STRINGS = [
    "KG3D_PhysxTerrain::LoadTerrain",
    "KG3D_PhysxTerrain::CreateEditInterface",
    "KG3D_PhysxTerrain::GetTileBBox",
    "PhysicsEngine::PhysicsManager::CreatePhysicsScene",
    "PhysicsEngine::PhysicsManager::CreatePhysXTerrain",
    "PhysicsEngine::PhysicsManager::CreatePhysicsSceneLoader",
    "PhysicsEngine::PhysicsManager::_InitPhysX",
    "PhysicsEngine::PhysicsScene::SweepEx",
    "RayCast",
    "GetFloorHeight",
    "PhysXRayCastCallback",
]

ADAPTER_STRINGS = [
    "PhysicsEngineX64.dll",
    "GetPhysicsManager",
    "KG3DEngineManager::_CreatePhysicsEngine",
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
    if off >= len(data):
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
    if off + 8 > len(data):
        return None
    return struct.unpack_from("<Q", data, off)[0]


def disasm_until_ret(md, secs, va, max_insn=60):
    name, base, data = sec_for_va(secs, va)
    if data is None or name.lower() != ".text":
        return [f"    (not in .text: {va:#x})"]
    lines = []
    for insn in md.disasm(data[va - base : va - base + 2048], va):
        text = f"    {insn.address:#x}: {insn.mnemonic:<8} {insn.op_str}"
        for op in insn.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                target = insn.address + insn.size + op.mem.disp
                s = read_cstr(secs, target)
                if s:
                    text += f"  ; \"{s[:70]}\""
        lines.append(text)
        if insn.mnemonic in ("ret", "int3") and len(lines) > 2:
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
    """Disassemble every executable section; match rip-mem and absolute imm operands."""
    refs = []
    for name, base, size, data in secs:
        if not name.lower().startswith(".text"):
            continue
        for insn in md.disasm(data, base):
            for op in insn.operands:
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                    t = insn.address + insn.size + op.mem.disp
                    if t in targets:
                        refs.append((insn.address, t, "ripmem"))
                elif op.type == X86_OP_IMM and op.imm in targets:
                    refs.append((insn.address, op.imm, "imm"))
    return refs


def main():
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True

    print("=" * 100)
    print(f"VTABLES in {PE.name}")
    pe, base, secs = load(PE)
    for label, va in VTABLES.items():
        print(f"\n---- {label} vtable @ {va:#x}")
        for i in range(16):
            fn = read_u64(secs, va + 8 * i)
            if not fn:
                print(f"  [{i:2}] 0")
                continue
            print(f"  [{i:2}] {fn:#x}")
            for line in disasm_until_ret(md, secs, fn, max_insn=14):
                print("      " + line.strip())

    print("\n---- global slots")
    for label, va in (("alloc-mgr", ALLOC_GLOBAL), ("file-system", FS_GLOBAL), ("manager-singleton", SINGLETON)):
        val = read_u64(secs, va)
        vt = read_u64(secs, val) if val else None
        print(f"  {label} @ {va:#x} = {val if val is None else hex(val)} vtable={vt if vt is None else hex(vt)}")

    print("\n" + "=" * 100)
    print(f"FUNCTION-NAME XREFS in {PE.name}")
    for needle in KEY_STRINGS:
        for sva in find_string_vas(secs, needle):
            print(f"\n---- \"{needle}\" @ {sva:#x}")
            refs = find_xrefs(md, secs, {sva})
            if not refs:
                print("  (no code xref)")
            for insn_va, _, kind in refs[:3]:
                print(f"  xref from {insn_va:#x} ({kind})")
                for line in disasm_until_ret(md, secs, insn_va - 96, max_insn=50):
                    print("  " + line)

    print("\n" + "=" * 100)
    print(f"ADAPTER XREFS in {ADAPTER.name}")
    pe2, base2, secs2 = load(ADAPTER)
    tvas = {}
    for needle in ADAPTER_STRINGS:
        for sva in find_string_vas(secs2, needle):
            tvas[sva] = needle
    for sva, needle in sorted(tvas.items()):
        print(f"  string \"{needle}\" @ {sva:#x}")
    refs = find_xrefs(md, secs2, set(tvas))
    print(f"\n{len(refs)} xrefs")
    for insn_va, target, kind in refs:
        print(f"\n---- xref \"{tvas[target]}\" from {insn_va:#x} ({kind})")
        for line in disasm_until_ret(md, secs2, insn_va - 128, max_insn=90):
            print("  " + line)


if __name__ == "__main__":
    main()
