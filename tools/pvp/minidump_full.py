#!/usr/bin/env python3
"""Streaming (mmap) reader for a large Windows minidump (e.g. the 5.6 GB W1 full dump).

`tools/camera/minidump_exc.py` reads the whole file into RAM; this one mmaps it so a multi-GB
dump can be inspected. Prints the module list, the thread list, and the faulting thread's CONTEXT,
and can read arbitrary process memory by RVA. Read-only.

Usage:
  python minidump_full.py <dump.dmp>                 # summary (modules, threads, contexts)
  python minidump_full.py <dump.dmp> --ctx           # + full register dump of every thread
  python minidump_full.py <dump.dmp> --mem 0xADDR    # hexdump the 64 bytes at a live VA
"""
from __future__ import annotations

import argparse
import mmap
import struct
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

STREAMS = {3: "ThreadList", 4: "ModuleList", 5: "Exception", 6: "MemoryList",
           7: "SystemInfo", 9: "Memory64List"}
# CONTEXT x64 register offsets (after the 6 Home + ContextFlags/MxCsr/Segs/EFlags header = 0x30)
CTX_REGS = [("Rax", 0x78), ("Rcx", 0x80), ("Rdx", 0x88), ("Rbx", 0x90), ("Rsp", 0x98),
            ("Rbp", 0xA0), ("Rsi", 0xA8), ("Rdi", 0xB0), ("R8", 0xB8), ("R9", 0xC0),
            ("R10", 0xC8), ("R11", 0xD0), ("R12", 0xD8), ("R13", 0xE0), ("R14", 0xE8),
            ("R15", 0xF0), ("Rip", 0xF8)]


def u32(d, o):
    return struct.unpack_from("<I", d.getvalue() if False else d, o)[0]


class Dump(object):
    def __init__(self, mm):
        self.mm = mm
        self.dir = {}
        nstreams, dira = struct.unpack_from("<II", mm, 8)
        for i in range(nstreams):
            t, size, rva = struct.unpack_from("<III", mm, dira + i * 12)
            self.dir[t] = (size, rva)
        self.modules = self._modules()
        self.threads = self._threads()
        self.mem64 = self._mem64()

    def _modules(self):
        out = []
        if 4 not in self.dir:
            return out
        _, rva = self.dir[4]
        n = struct.unpack_from("<I", self.mm, rva)[0]
        for i in range(n):
            o = rva + 4 + i * 108
            base, size = struct.unpack_from("<QI", self.mm, o)
            namerva = struct.unpack_from("<I", self.mm, o + 20)[0]
            nl = struct.unpack_from("<I", self.mm, namerva)[0]
            name = self.mm[namerva + 4:namerva + 4 + nl].decode("utf-16-le", "replace")
            out.append((base, size, name.rsplit("\\", 1)[-1]))
        return out

    def _threads(self):
        out = []
        if 3 not in self.dir:
            return out
        _, rva = self.dir[3]
        n = struct.unpack_from("<I", self.mm, rva)[0]
        for i in range(n):
            o = rva + 4 + i * 48
            tid = struct.unpack_from("<I", self.mm, o)[0]
            stack_start = struct.unpack_from("<Q", self.mm, o + 16)[0]
            ctx_size, ctx_rva = struct.unpack_from("<II", self.mm, o + 40)
            out.append((tid, stack_start, ctx_rva, ctx_size))
        return out

    def _mem64(self):
        # stream 9: u64 count, u64 baseRva, then descriptors {u64 start, u64 size}
        out = []
        if 9 not in self.dir:
            return out
        _, rva = self.dir[9]
        n, baserva = struct.unpack_from("<QQ", self.mm, rva)
        for i in range(n):
            start, size = struct.unpack_from("<QQ", self.mm, rva + 16 + i * 16)
            out.append((start, size, baserva))
            baserva += size
        return out

    def read(self, va, n):
        for start, size, foff in self.mem64:
            if start <= va < start + size:
                off = foff + (va - start)
                return self.mm[off:off + min(n, start + size - va)]
        return b""

    def resolve(self, va):
        for base, size, name in self.modules:
            if base <= va < base + size:
                return "%s+0x%X" % (name, va - base)
        return hex(va)

    def ctx(self, ctx_rva):
        return {name: struct.unpack_from("<Q", self.mm, ctx_rva + off)[0] for name, off in CTX_REGS}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dump")
    ap.add_argument("--ctx", action="store_true")
    ap.add_argument("--mem", default="")
    args = ap.parse_args()

    with open(args.dump, "rb") as f:
        mm = mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ)
        if mm[:4] != b"MDMP":
            print("not a minidump (no MDMP signature)")
            return 1
        d = Dump(mm)
        print("streams:", {STREAMS.get(k, k): v[0] for k, v in d.dir.items()})
        print("modules: %d" % len(d.modules))
        for base, size, name in d.modules[:200]:
            print("  %016X %10d  %s" % (base, size, name))
        print("threads: %d" % len(d.threads))
        # faulting thread = the one whose context Rip is in ntdll/engine after an AV (best-effort:
        # print all contexts if --ctx, else summarise rip for each)
        for tid, sstart, crva, csize in d.threads[:40]:
            c = d.ctx(crva)
            print("  tid=%d rsp=%s rip=%s rbp=%s" % (tid, d.resolve(c["Rsp"]), d.resolve(c["Rip"]),
                                                      d.resolve(c["Rbp"])))
            if args.ctx:
                for name, _ in CTX_REGS:
                    print("     %-4s %016X" % (name, c[name]))
        if args.mem:
            va = int(args.mem, 0)
            b = d.read(va, 64)
            print("mem @ %s: %s" % (d.resolve(va), b.hex()))
        mm.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
