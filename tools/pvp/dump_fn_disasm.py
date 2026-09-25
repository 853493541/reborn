#!/usr/bin/env python3
"""Disassemble functions that reference a list of method-name strings.

One linear pass over the executable sections (skipdata), then for every needle:
  - collect rip-relative xrefs to the string literal(s)
  - walk back to a 0xCC CC padding boundary -> function start
  - emit instructions from start to the first terminal ret/int3 (bounded)

Usage:
  python dump_fn_disasm.py <binary> --names names.txt --out-dir dir [--limit 220]
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import pefile
from capstone import CS_ARCH_X86, CS_MODE_64, Cs

IMAGE_SCN_CNT_CODE = 0x00000020
IMAGE_SCN_MEM_EXECUTE = 0x20000000


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("binary")
    ap.add_argument("--names", required=True, help="file with one needle per line")
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--limit", type=int, default=220, help="max instructions per fn")
    args = ap.parse_args()

    out_dir = Path(args.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    pe = pefile.PE(args.binary, fast_load=False)
    base = pe.OPTIONAL_HEADER.ImageBase
    data = pe.get_memory_mapped_image()
    text = []
    for s in pe.sections:
        if s.Characteristics & (IMAGE_SCN_CNT_CODE | IMAGE_SCN_MEM_EXECUTE):
            text.append((base + s.VirtualAddress, s.get_data()))

    needles = [n.strip() for n in Path(args.names).read_text(encoding="utf-8").splitlines()
               if n.strip() and not n.startswith("#")]

    # locate string VAs for each needle
    str_vas: dict[str, list[int]] = {}
    for n in needles:
        b = n.encode("latin-1", "replace")
        vas = []
        pos = 0
        while True:
            pos = data.find(b, pos)
            if pos < 0:
                break
            if pos == 0 or data[pos - 1] == 0:
                vas.append(base + pos)
            pos += 1
        str_vas[n] = vas

    # one pass: collect xrefs of interest
    md = Cs(CS_ARCH_X86, CS_MODE_64)
    md.detail = True
    md.skipdata = True
    xrefs: dict[str, list[int]] = {n: [] for n in needles}
    want: dict[int, str] = {}
    for n, vas in str_vas.items():
        for va in vas:
            want[va] = n
    if want:
        for sva, sdata in text:
            for insn in md.disasm(sdata, sva):
                if not insn.id:
                    continue
                try:
                    for op in insn.operands:
                        if op.type == 3 and op.mem.base == 41:
                            tgt = insn.address + insn.size + op.mem.disp
                            if tgt in want and len(xrefs[want[tgt]]) < 6:
                                xrefs[want[tgt]].append(insn.address)
                except Exception:
                    pass

    md2 = Cs(CS_ARCH_X86, CS_MODE_64)
    md2.detail = True
    md2.skipdata = True

    index = []
    for n in needles:
        xs = sorted(set(xrefs[n]))
        fname = "".join(ch if ch.isalnum() or ch in "._-" else "_" for ch in n)[:120]
        path = out_dir / f"{fname}.txt"
        lines = [f"# needle: {n}",
                 f"# string VAs: {[hex(v) for v in str_vas[n]]}",
                 f"# xrefs: {len(xs)}"]
        if not xs:
            lines.append("(no xref found)")
            (out_dir / f"{fname}.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
            index.append((n, 0, str(path)))
            continue
        for xa in xs:
            start = xa
            i = xa - base - 1
            limit = max(0, xa - base - 0x10000)
            while i > limit:
                if data[i] == 0xCC and data[i - 1] == 0xCC:
                    start = base + i + 1
                    break
                i -= 1
            lines.append(f"\n## xref {xa:#x}  fn_start {start:#x}")
            # find section containing start
            sva = sdata = None
            for sva2, sdata2 in text:
                if sva2 <= start < sva2 + len(sdata2):
                    sva, sdata = sva2, sdata2
                    break
            if sva is None:
                continue
            off = start - sva
            count = 0
            pad = 0
            for insn in md2.disasm(sdata[off:off + 0x3000], start):
                lines.append(f"{insn.address:#012x}: {insn.mnemonic}\t{insn.op_str}")
                count += 1
                if not insn.id or insn.mnemonic in ("int3",):
                    pad += 1
                else:
                    pad = 0
                if count >= args.limit or pad >= 8:
                    break
            if count >= args.limit:
                lines.append("...(truncated)")
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        index.append((n, len(xs), str(path)))

    print(f"{len(needles)} needles")
    for n, nx, p in index:
        print(f"  {n}: xrefs={nx} -> {p}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
