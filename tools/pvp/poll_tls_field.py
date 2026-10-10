import ctypes, ctypes.wintypes as w, struct, time, sys

k32 = ctypes.WinDLL('kernel32', use_last_error=True)
ntdll = ctypes.WinDLL('ntdll')
for fn in ('OpenThread', 'OpenProcess', 'CreateToolhelp32Snapshot'):
    getattr(k32, fn).restype = ctypes.c_void_p
k32.ReadProcessMemory.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p]
ntdll.NtQueryInformationThread.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_ulong, ctypes.c_void_p]


class TBI(ctypes.Structure):
    _fields_ = [("ExitStatus", ctypes.c_long), ("TebBaseAddress", ctypes.c_void_p),
                ("UniqueProcess", ctypes.c_void_p), ("UniqueThread", ctypes.c_void_p),
                ("AffinityMask", ctypes.c_void_p), ("Priority", ctypes.c_long), ("BasePriority", ctypes.c_long)]


class TE32(ctypes.Structure):
    _fields_ = [("dwSize", w.DWORD), ("cntUsage", w.DWORD), ("th32ThreadID", w.DWORD),
                ("th32OwnerProcessID", w.DWORD), ("tpBasePri", w.LONG), ("tpDeltaPri", w.LONG), ("dwFlags", w.DWORD)]


class ME32(ctypes.Structure):
    _fields_ = [("dwSize", w.DWORD), ("th32ModuleID", w.DWORD), ("th32ProcessID", w.DWORD),
                ("GlblcntUsage", w.DWORD), ("ProccntUsage", w.DWORD), ("modBaseAddr", ctypes.c_void_p),
                ("modBaseSize", w.DWORD), ("hModule", ctypes.c_void_p), ("szModule", ctypes.c_char * 256),
                ("szExePath", ctypes.c_char * 260)]


pid = int(sys.argv[1]); dur = float(sys.argv[2]) if len(sys.argv) > 2 else 85.0
TLSIDX_RVA = 0x26546C4
OFF = 0x89B0
hp = k32.OpenProcess(0x410, False, pid)


def rd(a, n):
    b = ctypes.create_string_buffer(n); got = ctypes.c_size_t(0)
    k32.ReadProcessMemory(hp, ctypes.c_void_p(a), b, n, ctypes.byref(got))
    return b.raw[:got.value]


def mod_base(name):
    for flags in (0x8 | 0x10, 0x8, 0x10):
        snap = k32.CreateToolhelp32Snapshot(flags, pid)
        if not snap or snap == -1:
            continue
        me = ME32(); me.dwSize = ctypes.sizeof(ME32)
        ok = k32.Module32First(snap, ctypes.byref(me)); base = 0
        names = []
        while ok:
            nm = me.szModule.decode('ascii', 'replace')
            names.append(nm)
            if name.lower() in nm.lower():
                base = int(me.modBaseAddr)
            ok = k32.Module32Next(snap, ctypes.byref(me))
        k32.CloseHandle(snap)
        if base:
            return base
        if names and flags == (0x8 | 0x10):
            print('modules seen (%d): %s' % (len(names), ', '.join(names[:40])), flush=True)
    return 0


eng = 0
for _ in range(40):
    eng = mod_base('KG3DEngineDX11EX64')
    if eng:
        break
    time.sleep(0.5)
b = rd(eng + TLSIDX_RVA, 4)
idx = struct.unpack('<I', b)[0] if len(b) == 4 else -1
print('engine base=%#x live _tls_index=%d (%#x)' % (eng, idx, idx), flush=True)


def field(tid, index, off):
    try:
        ht = k32.OpenThread(0x0040, False, tid)
        if not ht:
            return None
        tbi = TBI(); r = ntdll.NtQueryInformationThread(ht, 0, ctypes.byref(tbi), ctypes.sizeof(tbi), None)
        k32.CloseHandle(ht)
        if r != 0 or not tbi.TebBaseAddress:
            return None
        b1 = rd(int(tbi.TebBaseAddress) + 0x58, 8)
        if len(b1) < 8:
            return None
        tlsarr = struct.unpack('<Q', b1)[0]
        b2 = rd(tlsarr + index * 8, 8)
        if len(b2) < 8:
            return None
        blk = struct.unpack('<Q', b2)[0]
        if not blk:
            return None
        b3 = rd(blk + off, 4)
        return struct.unpack('<i', b3)[0] if len(b3) == 4 else None
    except Exception:
        return None


def tids():
    snap = k32.CreateToolhelp32Snapshot(4, 0)
    te = TE32(); te.dwSize = ctypes.sizeof(TE32); out = []
    ok = k32.Thread32First(snap, ctypes.byref(te))
    while ok:
        if te.th32OwnerProcessID == pid:
            out.append(te.th32ThreadID)
        ok = k32.Thread32Next(snap, ctypes.byref(te))
    k32.CloseHandle(snap)
    return out


prev = {}; t0 = time.time()
print('polling [TLSblock(%d)+0x%X] for %.0fs' % (idx, OFF, dur), flush=True)
while time.time() - t0 < dur:
    for tid in tids():
        v = field(tid, idx, OFF)
        if v is None:
            continue
        if prev.get(tid) != v:
            print('%7.1fs tid=%-6d =%#010x (%d)' % (time.time() - t0, tid, v & 0xffffffff, v), flush=True)
            prev[tid] = v
    time.sleep(0.12)
print('done', flush=True)
