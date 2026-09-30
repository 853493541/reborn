"""Minimal minidump (MDMP) exception/stack reader - stdlib only.

Usage: python minidump_exc.py <dump.dmp> [--strings]

Prints the exception record (code, address, thread), the faulting thread's
registers, the module map, the RBP-chain call stack (resolved to module+RVA),
and candidate return addresses on the faulting thread's stack. Built for the
D6 analysis (docs/camera/HOST_DEVIATIONS.md D6) since no debugger is installed.
"""

import os
import re
import struct
import sys


def u32(b, o):
    return struct.unpack_from('<I', b, o)[0]


def u64(b, o):
    return struct.unpack_from('<Q', b, o)[0]


def read_md_string(data, rva):
    if not rva:
        return ''
    n = u32(data, rva)
    return data[rva + 4:rva + 4 + n].decode('utf-16-le', 'replace')


class Dump(object):
    def __init__(self, data):
        self.data = data
        self.streams = {}
        nstreams = u32(data, 8)
        dira = u32(data, 12)
        for i in range(nstreams):
            o = dira + i * 12
            self.streams[u32(data, o)] = (u32(data, o + 4), u32(data, o + 8))
        self.modules = []
        if 4 in self.streams:
            _, rva = self.streams[4]
            n = u32(data, rva)
            for i in range(n):
                o = rva + 4 + i * 108
                base = u64(data, o)
                size = u32(data, o + 8)
                self.modules.append((base, size,
                                     os.path.basename(read_md_string(data, u32(data, o + 20)))))
        # address -> (dump offset, size)
        self.ranges = []
        if 9 in self.streams:  # Memory64List
            _, rva = self.streams[9]
            n = u64(data, rva)
            base_rva = u64(data, rva + 8)
            o = rva + 16
            off = base_rva
            for i in range(n):
                start = u64(data, o + i * 16)
                size = u64(data, o + i * 16 + 8)
                self.ranges.append((start, size, off))
                off += size
        elif 5 in self.streams:  # MemoryList (32-bit descriptors)
            _, rva = self.streams[5]
            n = u32(data, rva)
            for i in range(n):
                o = rva + 4 + i * 16
                start = u64(data, o)
                size = u32(data, o + 8)
                drva = u32(data, o + 12)
                self.ranges.append((start, size, drva))

    def read(self, addr, n):
        for start, size, off in self.ranges:
            if start <= addr and addr + n <= start + size:
                return self.data[off + (addr - start): off + (addr - start) + n]
        return None

    def resolve(self, v):
        for base, size, name in self.modules:
            if base <= v < base + size:
                return '%s+0x%X' % (name, v - base)
        return None

    def faulting_thread(self, tid):
        if 3 not in self.streams:
            return None
        data = self.data
        _, rva = self.streams[3]
        n = u32(data, rva)
        for i in range(n):
            o = rva + 4 + i * 48
            if u32(data, o) != tid:
                continue
            return (u64(data, o + 16), u32(data, o + 24), u32(data, o + 28))
        return None


def main():
    path = sys.argv[1]
    want_strings = '--strings' in sys.argv
    data = open(path, 'rb').read()
    if data[:4] != b'MDMP':
        print('not a minidump')
        return 2
    d = Dump(data)

    exc_code = exc_addr = None
    tid = None
    ctx = b''
    if 6 in d.streams:
        _, rva = d.streams[6]
        tid = u32(data, rva)
        er = rva + 8
        exc_code = u32(data, er)
        exc_addr = u64(data, er + 16)
        cs, cr = u32(data, er + 152), u32(data, er + 156)
        ctx = data[cr:cr + cs]

    print('dump   %s (%d bytes, %d modules, %d memory ranges)' % (
        os.path.basename(path), len(data), len(d.modules), len(d.ranges)))
    print('exc    code=0x%X addr=0x%X (%s) tid=%s' % (
        exc_code or 0, exc_addr or 0, d.resolve(exc_addr or 0) or '?', tid))

    regs = {}
    if ctx:
        for name, off in (('rax', 0x78), ('rcx', 0x80), ('rdx', 0x88), ('rbx', 0x90),
                          ('rsp', 0x98), ('rbp', 0xA0), ('rsi', 0xA8), ('rdi', 0xB0),
                          ('rip', 0xF8)):
            if len(ctx) >= off + 8:
                regs[name] = u64(ctx, off)
        print('regs   ' + ' '.join('%s=0x%X' % (k, regs[k]) for k in
                                   ('rip', 'rsp', 'rbp', 'rcx', 'rdx', 'rbx', 'rsi', 'rdi')))

    print('-- strings at register pointers --')
    for name in ('rcx', 'rdx', 'rbx', 'rdi', 'rsi'):
        p = regs.get(name)
        if not p:
            continue
        blob = d.read(p, 0x200) or b''
        for m in re.finditer(rb'[\x20-\x7e]{6,}', blob):
            print('  %s+0x%X: %s' % (name, m.start(), m.group(0).decode('ascii', 'replace')))
        for m in re.finditer(rb'(?:[\x20-\x7e]\x00){6,}', blob):
            print('  %s+0x%X: %s' % (name, m.start(), m.group(0).decode('utf-16-le', 'replace')))

    print('-- rbp-chain frames --')
    rbp = regs.get('rbp', 0)
    for i in range(48):
        if not rbp:
            break
        nxt = d.read(rbp, 16)
        if not nxt:
            print('  [broken: rbp=0x%X unreadable]' % rbp)
            break
        saved = u64(nxt, 0)
        ret = u64(nxt, 8)
        if not ret:
            break
        print('  #%02d ret=0x%016X %s' % (i, ret, d.resolve(ret) or '(unmapped)'))
        if saved <= rbp:
            break
        rbp = saved

    print('-- stack return-address candidates (engine modules) --')
    rsp = regs.get('rsp', 0)
    if rsp:
        seen = set()
        for i in range(rsp, rsp + 0x40000, 8):
            w = d.read(i, 8)
            if not w:
                break
            v = u64(w, 0)
            r = d.resolve(v)
            if r and any(k in r for k in ('KG3DEngineDX11EX64', 'KGCommonX64', 'KG_EngineEditor')):
                if r not in seen:
                    seen.add(r)
                    print('  [0x%X] 0x%016X %s' % (i, v, r))
                if len(seen) >= 30:
                    break
    if want_strings and rsp:
        print('-- printable strings near rsp --')
        lo, hi = rsp - 0x2000, rsp + 0x40000
        blob = d.read(lo, hi - lo) or b''
        for m in re.finditer(rb'[\x20-\x7e]{6,}', blob):
            s = m.group(0).decode('ascii', 'replace')
            if any(k in s.lower() for k in ('.mesh', '.dds', '.t2', 'datastore', 'material',
                                            '.pss', '.ini', '.txt', '.krl', '.pdb', '\\')):
                print('  @0x%X %s' % (lo + m.start(), s))
    return 0


if __name__ == '__main__':
    sys.exit(main())
