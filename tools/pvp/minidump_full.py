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


REG = ["Rax", "Rcx", "Rdx", "Rbx", "Rsp", "Rbp", "Rsi", "Rdi",
       "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15"]


def pe_exc_dir(hdr):
    """(exc_dir_rva, exc_dir_size) from a PE32+ header image (bytes)."""
    if hdr[:2] != b"MZ":
        return (0, 0)
    e = struct.unpack_from("<I", hdr, 0x3C)[0]
    opt = e + 0x18
    if struct.unpack_from("<H", hdr, opt)[0] != 0x20B:
        return (0, 0)
    return struct.unpack_from("<II", hdr, opt + 0x70 + 3 * 8)


def qword(b, o):
    return struct.unpack_from("<Q", b, o)[0]


class Unwinder(object):
    def __init__(self, dump):
        self.d = dump
        self._hdr = {}

    def hdr(self, base):
        if base not in self._hdr:
            self._hdr[base] = self.d.read(base, 0x400)
        return self._hdr[base]

    def find_rf(self, base, rip):
        hdr = self.hdr(base)
        exc_rva, exc_size = pe_exc_dir(hdr)
        if not exc_rva:
            return None
        data = self.d.read(base + exc_rva, exc_size)
        target = rip - base
        lo, hi = 0, len(data) // 12 - 1
        while lo <= hi:
            mid = (lo + hi) // 2
            b, en, ui = struct.unpack_from("<III", data, mid * 12)
            if target < b:
                hi = mid - 1
            elif target >= en:
                lo = mid + 1
            else:
                return (b, en, ui)
        return None

    def unwind_info(self, base, ui_rva):
        u = self.d.read(base + ui_rva, 0x180)
        vf = u[0]
        flags = vf >> 3
        prolog = u[1]
        ncode = u[2]
        fr = u[3]
        framereg = fr & 0xF
        frameoff = fr >> 4
        codes = []
        i = 4
        consumed = 0
        # CountOfCodes counts 2-byte SLOTS; ops with inline data consume extra slots.
        while consumed < ncode * 2:
            co = u[i]
            op = u[i + 1]
            i += 2
            consumed += 2
            opinfo = op >> 4
            opcode = op & 0xF
            extra = 0
            if opcode == 1 and opinfo == 0:
                extra = struct.unpack_from("<H", u, i)[0]
                i += 2
                consumed += 2
            elif opcode == 1 and opinfo == 1:
                extra = struct.unpack_from("<I", u, i)[0]
                i += 4
                consumed += 4
            elif opcode in (4, 8):
                extra = struct.unpack_from("<H", u, i)[0]
                i += 2
                consumed += 2
            elif opcode in (5, 9):
                extra = struct.unpack_from("<I", u, i)[0]
                i += 4
                consumed += 4
            codes.append((co, opcode, opinfo, extra))
        chained = None
        if flags & 4:  # UNW_FLAG_CHAININFO: a RUNTIME_FUNCTION follows the (4-aligned) codes
            off = (i + 3) & ~3
            cb, ce, cui = struct.unpack_from("<III", u, off)
            chained = (base, cb, ce, cui)
        return flags, prolog, framereg, frameoff, codes, chained, i

    def virtual_unwind(self, base, funcstart, ctx, codes, framereg, frameoff,
                       applyall=False, finalize=True):
        rsp = ctx["Rsp"]
        regs = dict(ctx)
        off = ctx["Rip"] - (base + funcstart)
        # UNWIND_CODE is stored in descending CodeOffset = reverse prolog order.
        for co, opcode, opinfo, extra in codes:
            if not applyall and co > off:   # not yet executed in the prolog
                continue
            if opcode == 0:                       # PUSH_NONVOL
                regs[REG[opinfo]] = qword(self.d.read(rsp, 8), 0)
                rsp += 8
            elif opcode == 1:                     # ALLOC_LARGE: OpInfo 0 = u16*8, 1 = u32 unscaled
                rsp += extra * 8 if opinfo == 0 else extra
            elif opcode == 2:                     # ALLOC_SMALL
                rsp += (opinfo + 1) * 8
            elif opcode == 3:                     # SET_FPREG
                rsp = regs[REG[framereg]] - frameoff * 16
            elif opcode in (4, 5):                # SAVE_NONVOL(_FAR)
                regs[REG[opinfo]] = qword(self.d.read(rsp + extra, 8), 0)
            elif opcode in (8, 9):                # SAVE_XMM128(_FAR): skip
                pass
            elif opcode == 10:                    # PUSH_MACHFRAME
                rsp += 0x30 if opinfo else 0x28
        if finalize:
            regs["Rip"] = qword(self.d.read(rsp, 8), 0)
            regs["Rsp"] = rsp + 8
        else:
            regs["Rsp"] = rsp
        return regs

    def walk(self, ctx, maxdepth=40):
        frames = [(ctx["Rip"], ctx["Rsp"], ctx["Rbp"])]
        for _ in range(maxdepth):
            rip = ctx["Rip"]
            mod = None
            for base, size, name in self.d.modules:
                if base <= rip < base + size:
                    mod = (base, size, name)
                    break
            if mod is None:
                break
            base = mod[0]
            rf = self.find_rf(base, rip)
            if rf is None:
                # no unwind info: assume a return address at rsp, rsp+=8
                ret = qword(self.d.read(ctx["Rsp"], 8), 0)
                ctx = dict(ctx)
                ctx["Rip"] = ret
                ctx["Rsp"] += 8
                frames.append((ret, ctx["Rsp"], ctx["Rbp"]))
                continue
            b, en, ui, = rf
            flags, prolog, framereg, frameoff, codes, chained, i = self.unwind_info(base, ui)
            if flags & 4 and chained:
                # chained: apply this fn's codes (no finalize), then the chained fn's, then finalize once
                ctx = self.virtual_unwind(base, b, ctx, codes, framereg, frameoff, finalize=False)
                cb, ce, cui = chained[1], chained[2], chained[3]
                _f, _p, _fr, _fo, codes2, _c, _i = self.unwind_info(base, cui)
                ctx = self.virtual_unwind(base, cb, ctx, codes2, _fr, _fo, applyall=True, finalize=True)
            else:
                ctx = self.virtual_unwind(base, b, ctx, codes, framereg, frameoff)
            frames.append((ctx["Rip"], ctx["Rsp"], ctx["Rbp"]))
            if ctx["Rip"] == 0:
                break
        return frames


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dump")
    ap.add_argument("--ctx", action="store_true")
    ap.add_argument("--mem", default="")
    ap.add_argument("--unwind", action="store_true")
    ap.add_argument("--tid", type=int, default=0)
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
        if args.unwind:
            w = Unwinder(d)
            tgt = None
            for tid, sstart, crva, csize in d.threads:
                c = d.ctx(crva)
                if args.tid:
                    if tid == args.tid:
                        tgt = (tid, c)
                        break
                elif d.resolve(c["Rip"]) == "ntdll.dll+0xFA7D":
                    tgt = (tid, c)
                    break
            if tgt is None:
                print("unwind: no target thread found (--tid?)")
            else:
                print("UNWIND tid=%d:" % tgt[0])
                for i, (rip, rsp, rbp) in enumerate(w.walk(tgt[1])):
                    print("  #%02d rip=%s rsp=%s rbp=%s" % (i, d.resolve(rip), hex(rsp), hex(rbp)))
        mm.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
