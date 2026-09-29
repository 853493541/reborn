#!/usr/bin/env python3
"""Parse the KPlayerClient protocol registration function.

Recovers the S2C protocol table from JX3LogicEditOperationX64.dll: the init
function writes, for every protocol id:

    lea rax, [rip + disp]            ; handler VA
    mov qword ptr [rdi + H + id*8], rax
    mov dword ptr [rdi + S + id*4], size   ; -1 = variable length

H and S are fixed table bases; the id is recovered by calibration against a
handler whose protocol id is known from another assert (arena competitor CD
state = 0x22A, handler 0x180192EE0).

Usage:
  python tools/netcode/parse_protocol_registration.py BINARY START END \
      [--handler 0xVA --id 0xNNN] [--out table.tsv]
"""
from __future__ import annotations

import argparse
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_OP_REG, X86_REG_RIP

IMAGE_SCN_CNT_CODE = 0x00000020
IMAGE_SCN_MEM_EXECUTE = 0x20000000


def load(path: Path):
    pe = pefile.PE(str(path), fast_load=False)
    base = pe.OPTIONAL_HEADER.ImageBase
    secs = []
    for s in pe.sections:
        secs.append(
            (
                base + s.VirtualAddress,
                max(s.SizeOfRawData, s.Misc_VirtualSize),
                s.get_data(),
            )
        )
    return pe, base, secs


def data_for(sections, va):
    for sva, vsize, data in sections:
        if sva <= va < sva + vsize:
            return data, sva
    return None, None


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("binary", type=Path)
    ap.add_argument("start", nargs="?", type=lambda x: int(x, 0))
    ap.add_argument("end", nargs="?", type=lambda x: int(x, 0))
    ap.add_argument("--handler", type=lambda x: int(x, 0), default=0x180192EE0)
    ap.add_argument("--id", dest="proto_id", type=lambda x: int(x, 0), default=0x22A)
    ap.add_argument("--no-calibrate", action="store_true")
    ap.add_argument("--out", type=Path)
    ap.add_argument("--names", type=Path, help="TSV of VA<TAB>name for annotation")
    args = ap.parse_args()

    pe, _, sections = load(args.binary)
    if args.start is None or args.end is None:
        text = max(
            (
                s
                for s in pe.sections
                if s.Characteristics & (IMAGE_SCN_CNT_CODE | IMAGE_SCN_MEM_EXECUTE)
            ),
            key=lambda s: s.Misc_VirtualSize,
        )
        base = pe.OPTIONAL_HEADER.ImageBase
        args.start = base + text.VirtualAddress
        args.end = args.start + max(text.SizeOfRawData, text.Misc_VirtualSize)
    data, sva = data_for(sections, args.start)
    if data is None:
        raise SystemExit("start not mapped")

    names: dict[int, str] = {}
    if args.names and args.names.exists():
        for line in args.names.read_text(encoding="utf-8", errors="replace").splitlines():
            if "\t" not in line:
                continue
            va, name = line.split("\t", 1)
            try:
                names[int(va, 0)] = name.strip()
            except ValueError:
                pass

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True

    handlers: dict[int, int] = {}
    sizes: dict[int, int] = {}
    pair_sizes: dict[int, int] = {}
    pending: int | None = None
    last_handler_slot: int | None = None

    code = data[args.start - sva: args.end - sva]
    for insn in md.disasm(code, args.start):
        ops = insn.operands
        if insn.mnemonic == "lea" and len(ops) == 2 and ops[0].type == X86_OP_REG:
            if ops[1].type == X86_OP_MEM and ops[1].mem.base == X86_REG_RIP:
                pending = insn.address + insn.size + ops[1].mem.disp
            else:
                pending = None
            continue
        if insn.mnemonic == "mov" and len(ops) == 2:
            dst, src = ops
            if (
                dst.type == X86_OP_MEM
                and dst.mem.base != 0
                and src.type == X86_OP_REG
                and pending is not None
                and insn.op_str.split(",")[0].strip().endswith("]")
            ):
                # store handler pointer: mov [reg+disp], rax
                handlers[int(dst.mem.disp)] = pending
                last_handler_slot = int(dst.mem.disp)
                pending = None
                continue
            if (
                dst.type == X86_OP_MEM
                and src.type == X86_OP_IMM
                and "dword ptr" in insn.op_str
            ):
                imm = int(src.imm)
                if imm >= 0x80000000:
                    imm -= 0x100000000
                sizes[int(dst.mem.disp)] = imm
                if last_handler_slot is not None:
                    pair_sizes[last_handler_slot] = int(dst.mem.disp)
                continue
            if (
                dst.type == X86_OP_MEM
                and src.type == X86_OP_IMM
                and "qword ptr" in insn.op_str
                and int(src.imm) in (-1, 0xFFFFFFFFFFFFFFFF)
            ):
                off = int(dst.mem.disp)
                sizes[off] = -1
                sizes[off + 4] = -1
                continue
        pending = None

    # Calibrate the handler table base H from the known pair.
    cal_slot = next((s for s, v in handlers.items() if v == args.handler), None)
    if args.no_calibrate:
        base_h = 0
    elif cal_slot is None:
        near = sorted(
            ((abs(v - args.handler), s, v) for s, v in handlers.items()),
        )[:8]
        info = "\n".join(f"  slot {s:#x} handler {v:#x} (delta {d:#x})" for d, s, v in near)
        raise SystemExit(
            f"calibration handler {args.handler:#x} not found; nearest:\n{info}"
        )
    else:
        base_h = cal_slot - args.proto_id * 8

    # Size table base S: size_slot = S + id*4, and id = (slot - base_h)/8, so
    # S = size_slot - (slot - base_h)/2 for every paired (handler slot, size slot).
    from collections import Counter

    s_votes: Counter[int] = Counter()
    for slot, size_slot in pair_sizes.items():
        s_votes[size_slot - (slot - base_h) // 2] += 1
    base_s = s_votes.most_common(1)[0][0] if s_votes else 0

    rows = []
    for slot, va in sorted(handlers.items()):
        pid = (slot - base_h) // 8
        size = sizes.get(base_s + pid * 4)
        rows.append((pid, va, size, names.get(va, "")))

    lines = [
        "# protocol registration table",
        f"# handler table base = {base_h:#x}",
        f"# size table base    = {base_s:#x}",
    ]
    lines.append("id\tslot\thandler_va\tsize\tname")
    for pid, va, size, name in rows:
        size_s = "" if size is None else ("var" if size < 0 else str(size))
        lines.append(f"{pid}\t{base_h + pid * 8:#x}\t{va:#x}\t{size_s}\t{name}")

    text = "\n".join(lines) + "\n"
    if args.out:
        args.out.write_text(text, encoding="utf-8")
        print(f"{len(rows)} handlers -> {args.out}; base_h={base_h:#x}")
    else:
        print(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
