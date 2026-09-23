#!/usr/bin/env python3
"""Build a catalog of KPlayerClient::Do* (client->server) protocol IDs.

Static approach: JX3 request builders follow the pattern

    mov eax, <protocol_id>
    mov word ptr [rsp+..], ax    ; packet header id
    ...
    mov r8d, <size>              ; or lea for some
    call KPlayerClient::SendPacket

We linearly disassemble the code sections once, remember the last string
reference to a `KPlayerClient::Do<Name>` assert, and when a call to the
SendPacket address is seen, extract the id/size from the recent instructions
and attribute the nearest preceding function start.

Output: TSV (call_addr, protocol_id, size, function, evidence)
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_OP_REG, X86_REG_RIP

sys.path.insert(0, str(Path(__file__).resolve().parent))
from extract_strings import extract  # noqa: E402

IMAGE_SCN_CNT_CODE = 0x00000020
IMAGE_SCN_MEM_EXECUTE = 0x20000000


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("binary", type=Path)
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--sendpacket", type=lambda v: int(v, 0), default=0x1801ACDA0,
                    help="VA of the SendPacket helper")
    ap.add_argument("--realsend", type=lambda v: int(v, 0), default=0x1801AC7D0)
    args = ap.parse_args(argv)

    pe = pefile.PE(str(args.binary), fast_load=False)
    base = pe.OPTIONAL_HEADER.ImageBase
    sections = []
    for s in pe.sections:
        sections.append({
            "name": s.Name.rstrip(b"\x00").decode("ascii", "replace"),
            "va": base + s.VirtualAddress,
            "data": s.get_data(),
            "code": bool(s.Characteristics & (IMAGE_SCN_CNT_CODE | IMAGE_SCN_MEM_EXECUTE)),
            "raw_off": s.PointerToRawData,
            "raw_size": s.SizeOfRawData,
        })

    def off_to_va(off):
        for s in sections:
            if s["raw_off"] <= off < s["raw_off"] + s["raw_size"]:
                return s["va"] + (off - s["raw_off"])
        return None

    def sec_for_va(va):
        for s in sections:
            if s["va"] <= va < s["va"] + len(s["data"]) + 0x1000:
                return s
        return None

    # 1. string VAs for KPlayerClient::Do*
    strings = extract(args.binary)
    do_names: dict[int, str] = {}
    for off, enc, s in strings:
        if s.startswith("KPlayerClient::Do") and s.isprintable() and " " not in s:
            va = off_to_va(off)
            if va is not None:
                do_names[va] = s

    # 2. linear scan
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    md.skipdata = True

    xrefs: list[tuple[int, str]] = []      # (insn_addr, name)
    calls: list[tuple[int, int | None, int | None, int]] = []  # (call_addr, id, size, last_xref)
    last_name: tuple[int, str] | None = None
    buf: list[dict] = []
    BUF = 160

    for sec in sections:
        if not sec["code"] or not sec["data"]:
            continue
        for insn in md.disasm(sec["data"], sec["va"]):
            if insn.id == 0:
                buf.append({"a": insn.address, "m": "db", "o": "", "id": None, "sz": None})
                if len(buf) > BUF:
                    buf.pop(0)
                continue
            rec = {"a": insn.address, "m": insn.mnemonic, "o": insn.op_str,
                   "id": None, "sz": None}
            # string xref?
            for op in insn.operands:
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                    t = insn.address + insn.size + op.mem.disp
                    if t in do_names:
                        last_name = (insn.address, do_names[t])
                        xrefs.append((insn.address, do_names[t]))
                elif op.type == X86_OP_IMM and op.imm in do_names:
                    last_name = (insn.address, do_names[op.imm])
                    xrefs.append((insn.address, do_names[op.imm]))
            # call SendPacket / RealSend?
            if insn.mnemonic == "call" and insn.operands and insn.operands[0].type == X86_OP_IMM:
                target = insn.operands[0].imm
                if target in (args.sendpacket, args.realsend):
                    pid = psize = None
                    # walk buffer backwards for size then id
                    mov_eax = None
                    for r in reversed(buf):
                        if mov_eax is None and r["m"] == "mov" and r["id"] is not None:
                            mov_eax = r["id"]
                        if r["m"] == "mov" and r["sz"] is not None and psize is None:
                            psize = r["sz"]
                        # id store: mov word ptr [rsp+..], ax
                        if (r["m"] == "mov" and r["o"].startswith("word ptr")
                                and r["o"].endswith(", ax")):
                            for rr in reversed(buf):
                                if rr["a"] >= r["a"]:
                                    continue
                                if rr["m"] == "mov" and rr["id"] is not None:
                                    pid = rr["id"]
                                    break
                            break
                    calls.append((insn.address, pid, psize, last_name[1] if last_name else ""))
            # decode mov eax, imm / mov r8d, imm
            if insn.mnemonic == "mov" and len(insn.operands) == 2:
                dst, src = insn.operands
                if dst.type == X86_OP_REG and src.type == X86_OP_IMM:
                    reg = insn.reg_name(dst.reg)
                    if reg in ("eax", "ax"):
                        rec["id"] = src.imm
                    elif reg in ("r8d", "r8w"):
                        rec["sz"] = src.imm
            buf.append(rec)
            if len(buf) > BUF:
                buf.pop(0)

    # 3. attribute function starts from xrefs
    starts: list[tuple[int, str]] = []
    for addr, name in xrefs:
        sec = sec_for_va(addr)
        start = addr
        if sec:
            data = sec["data"]
            off = addr - sec["va"]
            i = off - 1
            limit = max(0, off - 0x8000)
            while i > limit:
                if data[i] == 0xCC and (i == 0 or data[i - 1] == 0xCC):
                    start = sec["va"] + i + 1
                    break
                i -= 1
        starts.append((start, name))
    starts.sort()

    def name_for(call_addr: int) -> str:
        import bisect
        idx = bisect.bisect_right([s for s, _ in starts], call_addr) - 1
        if idx >= 0:
            return starts[idx][1]
        return ""

    lines = ["call_addr\tprotocol_id\tsize\tfunction\tsource"]
    seen = set()
    for call_addr, pid, psize, last in calls:
        fn = name_for(call_addr)
        if not fn:
            fn = last
        key = (fn, pid)
        if key in seen:
            continue
        seen.add(key)
        pid_s = f"0x{pid:04X}" if isinstance(pid, int) else "?"
        sz_s = f"0x{psize:X}" if isinstance(psize, int) else "?"
        lines.append(f"{call_addr:#x}\t{pid_s}\t{sz_s}\t{fn}\t{args.binary.name}")

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{len(lines)-1} unique (function, protocol) rows -> {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
