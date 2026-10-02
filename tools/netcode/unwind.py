import os
import struct

import pefile


REG_NAMES = ["RAX", "RCX", "RDX", "RBX", "RSP", "RBP", "RSI", "RDI",
             "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15"]


class UnwindInfo(object):
    def __init__(self, version, flags, prolog, frame_reg, frame_off, codes, chain_begin=None, chain=None):
        self.version = version
        self.flags = flags
        self.prolog = prolog
        self.frame_reg = frame_reg
        self.frame_off = frame_off
        self.codes = codes
        self.chain_begin = chain_begin
        self.chain = chain


class PEModule(object):
    def __init__(self, path, base):
        self.path = path
        self.base = base
        self.pe = pefile.PE(path, fast_load=True)
        self.data = open(path, "rb").read()
        self.sections = [(s.VirtualAddress, max(s.SizeOfRawData, s.Misc_VirtualSize), s.PointerToRawData)
                         for s in self.pe.sections]
        self.image_size = self.pe.OPTIONAL_HEADER.SizeOfImage
        self.pdata = []
        self.unwind_cache = {}
        self._load_pdata()

    def rva_to_off(self, rva):
        for va, sz, praw in self.sections:
            if va <= rva < va + sz:
                return praw + (rva - va)
        return None

    def _load_pdata(self):
        for s in self.pe.sections:
            if s.Name.rstrip(b"\x00") == b".pdata":
                off = s.PointerToRawData
                n = s.SizeOfRawData // 12
                for i in range(n):
                    b, e, u = struct.unpack_from("<III", self.data, off + i * 12)
                    if b == 0 and e == 0:
                        continue
                    self.pdata.append((b, e, u))
                self.pdata.sort()
                break

    def find_function(self, rva):
        lo, hi = 0, len(self.pdata) - 1
        while lo <= hi:
            mid = (lo + hi) // 2
            b, e, u = self.pdata[mid]
            if rva < b:
                hi = mid - 1
            elif rva >= e:
                lo = mid + 1
            else:
                return b, e, u
        return None

    def decode_unwind(self, ui_rva):
        if ui_rva in self.unwind_cache:
            return self.unwind_cache[ui_rva]
        off = self.rva_to_off(ui_rva)
        if off is None:
            self.unwind_cache[ui_rva] = None
            return None
        b0, b1, b2, b3 = struct.unpack_from("<BBBB", self.data, off)
        version = b0 & 0x7
        flags = b0 >> 3
        prolog = b1
        count = b2
        frame_reg = b3 & 0xF
        frame_off = b3 >> 4
        codes = []
        pos = off + 4
        i = 0
        while i < count:
            co, ui = struct.unpack_from("<BB", self.data, pos)
            pos += 2
            op = ui & 0xF
            info = ui >> 4
            extra = 0
            if op == 1:
                if info == 0:
                    extra = struct.unpack_from("<H", self.data, pos)[0]
                    pos += 2
                    i += 1
                else:
                    extra = struct.unpack_from("<I", self.data, pos)[0]
                    pos += 4
                    i += 2
            elif op in (4, 6):
                extra = struct.unpack_from("<H", self.data, pos)[0]
                pos += 2
                i += 1
            elif op in (5, 7):
                extra = struct.unpack_from("<I", self.data, pos)[0]
                pos += 4
                i += 2
            codes.append((co, op, info, extra))
            i += 1
        chain_begin = None
        chain = None
        if flags & 4:
            if pos % 4:
                pos += 4 - (pos % 4)
            cb, ce, cu = struct.unpack_from("<III", self.data, pos)
            chain_begin = cb
            chain = self.decode_unwind(cu)
        info_obj = UnwindInfo(version, flags, prolog, frame_reg, frame_off, codes, chain_begin, chain)
        self.unwind_cache[ui_rva] = info_obj
        return info_obj


def unwind_frame(modules_by_base, read_mem, rip, rsp, regs):
    mod = None
    for base, m in modules_by_base:
        if base <= rip < base + m.image_size:
            if mod is None or base > mod[0]:
                mod = (base, m)
    if mod is None:
        return None, "module not loaded"
    base, m = mod
    rva = rip - base
    fn = m.find_function(rva)
    regs = dict(regs)
    regs["RSP"] = rsp
    if fn is None:
        val = read_mem(rsp, 8)
        if val is None:
            return None, "leaf read failed @0x%X" % rsp
        return (struct.unpack("<Q", val)[0], rsp + 8, regs), None
    _, _, ui_rva = fn
    ui = m.decode_unwind(ui_rva)
    if ui is None:
        return None, "no unwind info"
    def apply_frame_reg(info_obj, off_in_fn):
        nonlocal rsp
        if info_obj.frame_reg != 0 and off_in_fn >= info_obj.prolog:
            fr = REG_NAMES[info_obj.frame_reg]
            if fr in regs and regs[fr] != 0:
                rsp = regs[fr] - info_obj.frame_off * 16
                regs["RSP"] = rsp

    def apply_codes(codes, off_in_fn):
        nonlocal rsp
        for co, op, info, extra in codes:
            if co > off_in_fn:
                continue
            if op == 0:
                rsp += 8
                v = read_mem(rsp, 8)
                if v is None:
                    return "push read failed @0x%X" % rsp
                regs[REG_NAMES[info]] = struct.unpack("<Q", v)[0]
                regs["RSP"] = rsp
            elif op == 1:
                rsp += extra * 8 if info == 0 else extra
                regs["RSP"] = rsp
            elif op == 2:
                rsp += info * 8 + 8
                regs["RSP"] = rsp
            elif op == 3:
                pass
            elif op == 4 or op == 5:
                v = read_mem(rsp + extra, 8)
                if v is not None:
                    regs[REG_NAMES[info]] = struct.unpack("<Q", v)[0]
            elif op == 8:
                rsp += 24 if info == 0 else 32
                regs["RSP"] = rsp
        return None

    apply_frame_reg(ui, rva - fn[0])
    err = apply_codes(ui.codes, rva - fn[0])
    if err:
        return None, err
    chain = ui.chain
    chain_begin = ui.chain_begin
    while chain is not None:
        chain_off = rva - chain_begin
        apply_frame_reg(chain, chain_off)
        err = apply_codes(chain.codes, chain_off)
        if err:
            return None, err
        chain_begin = chain.chain_begin
        chain = chain.chain
    v = read_mem(rsp, 8)
    if v is None:
        return None, "return read failed @0x%X" % rsp
    return (struct.unpack("<Q", v)[0], rsp + 8, regs), None

