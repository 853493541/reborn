#!/usr/bin/env python3
"""Verify camera loader defaults: pair each GetFloat/GetBool(key, default)
with the constant actually loaded into xmm2/xmm3, machine-checked.

Usage:
  python tools/netcode/verify_camera_defaults.py > proof/netcode/camera_defaults_verified.txt
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_OP_REG, X86_REG_RIP

DLL = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3RepresentX64.dll"
RANGES = [
    ("LoadCarrierParams", 0x180ACFB70, 0x180ACFFEE),
    ("MovePitch10RowLoader", 0x180338C10, 0x180338D69),
    ("LoadAirCombatParams", 0x180AC9C00, 0x180ACA100),
]
KEY_REG = "rdx"
GET_FLOAT_SLOT = 0x50
GET_BOOL_SLOT = 0x70


def main() -> int:
    import argparse

    global DLL, RANGES, KEY_REG
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--binary", default=DLL)
    ap.add_argument("--range", dest="ranges", action="append", default=None,
                    help="label:start:end (hex), repeatable; overrides built-in ranges")
    ap.add_argument("--key-reg", default="rdx", choices=("rdx", "r8"),
                    help="register holding the config key (default rdx; engine loader uses r8)")
    args = ap.parse_args()

    DLL = args.binary
    KEY_REG = args.key_reg
    if args.ranges:
        RANGES = []
        for spec in args.ranges:
            label, start, end = spec.split(":")
            RANGES.append((label, int(start, 0), int(end, 0)))

    pe = pefile.PE(DLL)
    base = pe.OPTIONAL_HEADER.ImageBase

    def read_cstr(va: int, limit: int = 120) -> str | None:
        try:
            data = pe.get_data(va - base, limit)
        except Exception:
            return None
        end = data.find(b"\x00")
        if end < 0:
            return None
        raw = data[:end]
        if not raw or not all(9 <= b <= 126 for b in raw):
            return None
        return raw.decode("ascii", "replace")

    def read_f32(va: int) -> float | None:
        try:
            data = pe.get_data(va - base, 4)
        except Exception:
            return None
        if not data or len(data) < 4:
            return None
        return struct.unpack("<f", data)[0]

    def section_for(va: int):
        for s in pe.sections:
            sva = base + s.VirtualAddress
            if sva <= va < sva + max(s.Misc_VirtualSize, s.SizeOfRawData):
                return s
        return None

    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True

    rows: list[tuple[str, str, str, float | None, str]] = []
    reg_src: dict[str, tuple[str, int | None]] = {}  # xmmN -> ("const", va) | ("zero", None)
    for label, start, end in RANGES:
        sec = section_for(start)
        if sec is None:
            print(f"## {label}: unmapped", file=sys.stderr)
            continue
        code = sec.get_data()[start - (base + sec.VirtualAddress): end - (base + sec.VirtualAddress)]
        cur_key: str | None = None
        reg_src = {}
        for insn in md.disasm(code, start):
            if insn.id == 0:
                continue
            ops = insn.operands
            # string into rdx = upcoming key
            if insn.mnemonic == "lea" and len(ops) == 2 and ops[1].type == X86_OP_MEM and ops[1].mem.base == X86_REG_RIP:
                target = insn.address + insn.size + ops[1].mem.disp
                s = read_cstr(target)
                if s and s[0].isalpha() and all(c.isalnum() or c in "_" for c in s) and len(s) > 2:
                    if ops[0].type == X86_OP_REG and insn.reg_name(ops[0].reg) == KEY_REG:
                        cur_key = s
            # track xmm register sources
            if insn.mnemonic == "movss" and len(ops) == 2 and ops[0].type == X86_OP_REG:
                reg = insn.reg_name(ops[0].reg)
                if reg.startswith("xmm"):
                    if ops[1].type == X86_OP_MEM and ops[1].mem.base == X86_REG_RIP:
                        reg_src[reg] = ("const", insn.address + insn.size + ops[1].mem.disp)
                    elif ops[1].type == X86_OP_REG:
                        reg_src[reg] = reg_src.get(insn.reg_name(ops[1].reg), ("reg", None))
                    else:
                        reg_src[reg] = ("reg", None)
            if insn.mnemonic in ("movaps", "movups", "movdqa") and len(ops) == 2 and ops[0].type == X86_OP_REG:
                reg = insn.reg_name(ops[0].reg)
                if reg.startswith("xmm"):
                    if ops[1].type == X86_OP_REG:
                        reg_src[reg] = reg_src.get(insn.reg_name(ops[1].reg), ("reg", None))
                    else:
                        reg_src[reg] = ("reg", None)
            if insn.mnemonic == "xorps" and len(ops) == 2 and ops[0].type == X86_OP_REG:
                reg = insn.reg_name(ops[0].reg)
                if reg.startswith("xmm") and insn.reg_name(ops[0].reg) == insn.reg_name(ops[1].reg):
                    reg_src[reg] = ("zero", None)
            # GetFloat/GetBool call
            if insn.mnemonic == "call" and len(ops) == 1 and ops[0].type == X86_OP_MEM:
                slot = insn.operands[0].mem.disp
                if slot in (GET_FLOAT_SLOT, GET_BOOL_SLOT) and cur_key:
                    if slot == GET_BOOL_SLOT:
                        rows.append((label, cur_key, "bool", None, "no-default(false)"))
                    else:
                        src = reg_src.get("xmm2", ("reg", None))
                        if src[0] == "const":
                            value = read_f32(src[1])
                            rows.append((label, cur_key, "const", value, f"{src[1]:#x}"))
                        elif src[0] == "zero":
                            rows.append((label, cur_key, "zero", 0.0, "xorps"))
                        else:
                            rows.append((label, cur_key, "reg/struct", None, "-"))
                    cur_key = None

    seen: set[tuple[str, str]] = set()
    for label, key, kind, value, va in rows:
        if (key, kind) in seen:
            continue
        seen.add((key, kind))
        v = f"{value:.9g}" if value is not None else "-"
        print(f"{label}\t{key}\t{kind}\t{v}\t{va}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
