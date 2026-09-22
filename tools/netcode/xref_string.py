#!/usr/bin/env python3
"""Find code xrefs to a string in a PE binary and disassemble around them.

Read-only recon for the JX3 netcode research: given an assert/log string (or any
substring), locate every RIP-relative reference in executable sections, guess
the enclosing function start, and emit annotated disassembly.

Usage:
  python tools/netcode/xref_string.py BINARY "KPlayerClient::DoHandshakeRequest" \
      --out proof/netcode/disasm/handshake.txt
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs
from capstone.x86 import X86_OP_MEM, X86_OP_IMM, X86_REG_RIP

sys.path.insert(0, str(Path(__file__).resolve().parent))
from extract_strings import extract  # noqa: E402

IMAGE_SCN_CNT_CODE = 0x00000020
IMAGE_SCN_MEM_EXECUTE = 0x20000000


def load(path: Path):
    pe = pefile.PE(str(path), fast_load=False)
    base = pe.OPTIONAL_HEADER.ImageBase
    sections = []
    for s in pe.sections:
        name = s.Name.rstrip(b"\x00").decode("ascii", "replace")
        sections.append(
            {
                "name": name,
                "va": base + s.VirtualAddress,
                "vsize": max(s.Misc_VirtualSize, s.SizeOfRawData),
                "raw_off": s.PointerToRawData,
                "raw_size": s.SizeOfRawData,
                "data": s.get_data(),
                "code": bool(s.Characteristics & (IMAGE_SCN_CNT_CODE | IMAGE_SCN_MEM_EXECUTE)),
            }
        )
    imports = {}
    if hasattr(pe, "DIRECTORY_ENTRY_IMPORT"):
        for entry in pe.DIRECTORY_ENTRY_IMPORT:
            dll = entry.dll.decode("ascii", "replace")
            for imp in entry.imports:
                if imp.name:
                    imports[imp.address] = f"{dll}!{imp.name.decode('ascii', 'replace')}"
    return pe, base, sections, imports


def sec_for_va(sections, va):
    for s in sections:
        if s["va"] <= va < s["va"] + s["vsize"]:
            return s
    return None


def off_to_va(sections, off):
    for s in sections:
        if s["raw_off"] <= off < s["raw_off"] + s["raw_size"]:
            return s["va"] + (off - s["raw_off"])
    return None


def read_cstr(sections, va, limit=300):
    s = sec_for_va(sections, va)
    if s is None:
        return None
    off = va - s["va"]
    data = s["data"]
    if off < 0 or off >= len(data):
        return None
    end = off
    while end < len(data) and data[end] != 0 and end - off < limit:
        end += 1
    raw = data[off:end]
    if not raw:
        return None
    printable = sum(1 for b in raw if 9 <= b <= 13 or 32 <= b <= 126)
    return raw.decode("ascii", "replace") if printable / len(raw) >= 0.9 else None


def find_function_start(sections, addr, max_back=0x8000):
    s = sec_for_va(sections, addr)
    if s is None:
        return addr
    off = addr - s["va"]
    data = s["data"]
    i = off - 1
    limit = max(0, off - max_back)
    while i > limit:
        if data[i] == 0xCC and (i == 0 or data[i - 1] == 0xCC):
            return s["va"] + i + 1
        i -= 1
    return max(s["va"], addr - 0x400)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("binary", type=Path)
    ap.add_argument("needle")
    ap.add_argument("--out", type=Path)
    ap.add_argument("--before", type=int, default=0, help="extra bytes to disasm before function start")
    ap.add_argument("--after", type=int, default=900, help="bytes to disasm after the xref")
    ap.add_argument("--all", action="store_true", help="include every matching string")
    ap.add_argument("--exact", action="store_true")
    args = ap.parse_args(argv)

    pe, base, sections, imports = load(args.binary)
    strings = extract(args.binary)
    matches = [
        (off, enc, s)
        for off, enc, s in strings
        if (s == args.needle if args.exact else args.needle in s)
    ]
    if not matches:
        print(f"string not found: {args.needle!r}", file=sys.stderr)
        return 2
    if not args.all:
        matches = matches[:1]

    code_secs = [s for s in sections if s["code"] and s["data"]]
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    md.skipdata = True

    str_vas: dict[int, tuple[int, str]] = {}
    for off, enc, s in matches:
        va = off_to_va(sections, off)
        if va is not None:
            str_vas[va] = (off, s)

    found: list[tuple[int, int, str]] = []
    for sec in code_secs:
        for insn in md.disasm(sec["data"], sec["va"]):
            if insn.id == 0:
                continue
            for op in insn.operands:
                if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                    target = insn.address + insn.size + op.mem.disp
                    if target in str_vas:
                        found.append((insn.address, target, str_vas[target][1]))
                elif op.type == X86_OP_IMM and op.imm in str_vas:
                    found.append((insn.address, op.imm, str_vas[op.imm][1]))

    lines = [f"# {args.binary.name} xrefs for {args.needle!r}", f"# image base {base:#x}", ""]
    seen_starts: set[int] = set()
    for insn_addr, str_va, text in found:
        start = find_function_start(sections, insn_addr) - args.before
        if start in seen_starts:
            continue
        seen_starts.add(start)
        end = insn_addr + args.after
        sec = sec_for_va(sections, start)
        lines.append(
            f"## xref @ {insn_addr:#x} -> {str_va:#x} {text!r}  fn start {start:#x}"
        )
        if sec is None:
            lines.append("   (unmapped)")
            continue
        off = start - sec["va"]
        code = sec["data"][off: off + (end - start)]
        for insn in md.disasm(code, start):
            note = ""
            if insn.id != 0:
                for op in insn.operands:
                    if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                        t = insn.address + insn.size + op.mem.disp
                        s = read_cstr(sections, t)
                        imp = imports.get(t)
                        if s:
                            note = f"  ; {s!r}"
                        elif imp:
                            note = f"  ; {imp}"
                    elif op.type == X86_OP_IMM:
                        imp = imports.get(op.imm)
                        s = read_cstr(sections, op.imm)
                        if s:
                            note = f"  ; {s!r}"
                        elif imp:
                            note = f"  ; {imp}"
            lines.append(f"  {insn.address:#x}: {insn.mnemonic:<8} {insn.op_str}{note}")
        lines.append("")

    text = "\n".join(lines) + "\n"
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8", errors="replace")
        print(f"{len(found)} xrefs / {len(seen_starts)} functions -> {args.out}")
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
