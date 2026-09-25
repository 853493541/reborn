"""Fourth-pass physics recon: manager vtable + brute-force RIP-relative string xrefs."""
from __future__ import annotations

import struct
from pathlib import Path

import numpy as np
import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_MEM, X86_REG_RIP

PE = Path(r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PhysicsEngineX64.dll")
MANAGER_OBJ = 0x18011B5D0

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
    "PhysicsEngine::_CreateSourceLoader",
    "PhysicsEngine::PhysicsShapeFactory::CreateShapeFromGeometryData",
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
    return raw.decode("ascii", "replace") if printable / len(raw) >= 0.9 else None


def read_u64(secs, va):
    name, base, data = sec_for_va(secs, va)
    if data is None:
        return None
    off = va - base
    if off < 0 or off + 8 > len(data):
        return None
    return struct.unpack_from("<Q", data, off)[0]


def brute_rip_refs(secs, target):
    """Find likely `lea/mov reg, [rip+disp32]` referencing target (vectorized)."""
    hits = []
    for name, base, size, data in secs:
        if not name.lower().startswith(".text"):
            continue
        raw = np.frombuffer(data, dtype=np.uint8)
        n = len(raw) - 6
        if n <= 0:
            continue
        win = np.lib.stride_tricks.as_strided(raw[3:], shape=(n, 4), strides=(1, 1))
        disp = (
            win[:, 0].astype(np.int64)
            + (win[:, 1].astype(np.int64) << 8)
            + (win[:, 2].astype(np.int64) << 16)
            + (win[:, 3].astype(np.int64) << 24)
        )
        disp = np.where(disp >= 1 << 31, disp - (1 << 32), disp)
        offsets = np.arange(len(raw), dtype=np.int64)[:n]
        match = base + offsets + 7 + disp == target
        hits.extend(int(base + i) for i in np.nonzero(match)[0])
    return hits


def disasm_from(md, secs, va, max_insn=80, back=0):
    name, base, data = sec_for_va(secs, va)
    if data is None or not name.lower().startswith(".text"):
        return [f"    (not code {va:#x})"]
    lines = []
    for insn in md.disasm(data[va - base : va - base + 4096], va):
        text = f"    {insn.address:#x}: {insn.mnemonic:<8} {insn.op_str}"
        for op in insn.operands:
            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                t = insn.address + insn.size + op.mem.disp
                s = read_cstr(secs, t)
                if s:
                    text += f"  ; \"{s[:80]}\""
        lines.append(text)
        if insn.mnemonic == "ret" and len(lines) > 3:
            break
        if len(lines) >= max_insn:
            lines.append("    ...")
            break
    return lines


def main():
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    pe, base, secs = load(PE)

    vptr = read_u64(secs, MANAGER_OBJ)
    print(f"manager obj @ {MANAGER_OBJ:#x} vptr={vptr:#x}")
    print("\n=== manager vtable ===")
    for i in range(32):
        fn = read_u64(secs, vptr + 8 * i)
        if not fn:
            print(f"  [{i:2}] 0")
            continue
        seg = sec_for_va(secs, fn)[0]
        print(f"  [{i:2}] {fn:#x} ({seg})")
        if seg and seg.lower().startswith(".text"):
            for line in disasm_from(md, secs, fn, max_insn=12):
                print("        " + line.strip())

    print("\n=== string xrefs (brute force) ===")
    for needle in KEY_STRINGS:
        nb = needle.encode() + b"\x00"
        sva = None
        for name, b, size, data in secs:
            j = data.find(nb)
            if j >= 0:
                sva = b + j
                break
        if sva is None:
            print(f"\n-- \"{needle}\" not in binary")
            continue
        refs = brute_rip_refs(secs, sva)
        print(f"\n-- \"{needle}\" @ {sva:#x} refs={len(refs)}")
        for r in refs[:2]:
            print(f"   ref from {r:#x}")
            for line in disasm_from(md, secs, r - 320, max_insn=110):
                print("   " + line)


if __name__ == "__main__":
    main()
