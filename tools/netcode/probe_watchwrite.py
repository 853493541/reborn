"""V2: find the writer of the startup gate object (sub+0x18) with a hardware
write-watchpoint on our own probe child.

Spawns JX3ClientX64.exe under DEBUG_ONLY_THIS_PROCESS, creates the PID-keyed launch
block before continuing, finds the WinMain state object via .pdata unwinding at ~0.9 s,
sets DR0 = sub+0x18 with a write breakpoint (DR7) on every thread, then reports the
first write: RIP, module, function start (.pdata) and the store bytes. Read-only
observation plus a debugger attach to our own child; no disk writes, no install changes.
"""
import ctypes
import ctypes.wintypes as w
import os
import struct
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, r"C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode")

from probe_state_timeline import (
    EXE, CWD, k32, BLOCK, NAME_FMT, INVALID_HANDLE_VALUE, FILE_MAP_ALL_ACCESS,
    STARTUPINFO, PROCESS_INFORMATION, build_block, module_list, CTX_OFF, CONTEXT_FULL,
)
from unwind import PEModule, unwind_frame

DEBUG_ONLY_THIS_PROCESS = 0x2
DBG_CONTINUE = 0x00010002
DBG_EXCEPTION_NOT_HANDLED = 0x80010001
EXCEPTION_DEBUG_EVENT = 1
CREATE_PROCESS_DEBUG_EVENT = 3
EXIT_PROCESS_DEBUG_EVENT = 5
LOAD_DLL_DEBUG_EVENT = 6
OUTPUT_DEBUG_STRING_EVENT = 8
EXCEPTION_BREAKPOINT = 0x80000003
EXCEPTION_SINGLE_STEP = 0x80000004
CONTEXT_DEBUG = CONTEXT_FULL | 0x00100010
DR0_OFF, DR6_OFF, DR7_OFF = 0x48, 0x68, 0x70
TH32CS_SNAPTHREAD = 0x4
THREAD_ACCESS = 0x0002 | 0x0004 | 0x0008 | 0x0010  # SUSPEND|RESUME|GET_CTX|SET_CTX


class EXCEPTION_RECORD(ctypes.Structure):
    _fields_ = [("ExceptionCode", w.DWORD), ("ExceptionFlags", w.DWORD),
                ("ExceptionRecord", ctypes.c_void_p), ("ExceptionAddress", ctypes.c_void_p),
                ("NumberParameters", w.DWORD), ("__align", w.DWORD),
                ("ExceptionInformation", ctypes.c_ulonglong * 15)]


class DEBUG_EVENT(ctypes.Structure):
    # the union is 8-byte aligned -> it starts at offset 16 (4 bytes of padding after
    # the three DWORDs); without the pad the union offsets are shifted and the handles
    # from CREATE_PROCESS_DEBUG_INFO are garbage
    _fields_ = [("dwDebugEventCode", w.DWORD), ("dwProcessId", w.DWORD),
                ("dwThreadId", w.DWORD), ("__pad", w.DWORD), ("u", ctypes.c_byte * 160)]


k32.WaitForDebugEvent.restype = w.BOOL
k32.WaitForDebugEvent.argtypes = [ctypes.POINTER(DEBUG_EVENT), w.DWORD]
k32.ContinueDebugEvent.restype = w.BOOL
k32.ContinueDebugEvent.argtypes = [w.DWORD, w.DWORD, w.DWORD]
k32.OpenThread.restype = w.HANDLE
k32.OpenThread.argtypes = [w.DWORD, w.BOOL, w.DWORD]
k32.SetThreadContext.restype = w.BOOL
k32.SetThreadContext.argtypes = [w.HANDLE, ctypes.c_void_p]
k32.SuspendThread.restype = w.DWORD
k32.SuspendThread.argtypes = [w.HANDLE]
k32.ResumeThread.restype = w.DWORD
k32.ResumeThread.argtypes = [w.HANDLE]
k32.CreateToolhelp32Snapshot.restype = w.HANDLE
k32.CreateToolhelp32Snapshot.argtypes = [w.DWORD, w.DWORD]


def read_mem(h, addr, size):
    buf = ctypes.create_string_buffer(size)
    n = ctypes.c_size_t()
    if k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, size, ctypes.byref(n)) and n.value == size:
        return buf.raw
    return None


def read_u64(h, addr):
    d = read_mem(h, addr, 8)
    return struct.unpack("<Q", d)[0] if d else None


MEM_COMMIT = 0x1000
PAGE_READABLE = {0x02, 0x04, 0x08, 0x20, 0x40, 0x80}
PAGE_GUARD = 0x100


class MBI(ctypes.Structure):
    _fields_ = [("BaseAddress", ctypes.c_void_p), ("AllocationBase", ctypes.c_void_p),
                ("AllocationProtect", w.DWORD), ("RegionSize", ctypes.c_size_t),
                ("State", w.DWORD), ("Protect", w.DWORD), ("Type", w.DWORD)]


k32.VirtualQueryEx.restype = ctypes.c_size_t
k32.VirtualQueryEx.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t]


def scan_for(h, pattern, max_mb=1024):
    hits = []
    addr = 0
    scanned = 0
    regions = 0
    while addr < 0x7FFFFFFFFFFF and scanned < max_mb * 1048576:
        mbi = MBI()
        if not k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
            break
        base = mbi.BaseAddress or 0
        size = mbi.RegionSize
        regions += 1
        prot = mbi.Protect & 0xFF
        if mbi.State == MEM_COMMIT and prot in PAGE_READABLE and not (mbi.Protect & PAGE_GUARD):
            off = 0
            carry = b""
            while off < size and scanned < max_mb * 1048576:
                n = min(4 << 20, size - off)
                data = read_mem(h, base + off, n)
                if data:
                    scanned += n
                    buf = carry + data
                    start = 0
                    while True:
                        i = buf.find(pattern, start)
                        if i < 0:
                            break
                        hits.append(base + off - len(carry) + i)
                        start = i + 1
                    carry = data[-8:]
                off += n
        addr = base + size
    scan_for.last = (regions, scanned)
    return hits


def threads_of(pid):
    class THREADENTRY32(ctypes.Structure):
        _fields_ = [("dwSize", w.DWORD), ("cntUsage", w.DWORD), ("th32ThreadID", w.DWORD),
                    ("th32OwnerProcessID", w.DWORD), ("tpBasePri", ctypes.c_long),
                    ("tpDeltaPri", ctypes.c_long), ("dwFlags", w.DWORD)]
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0)
    te = THREADENTRY32()
    te.dwSize = ctypes.sizeof(THREADENTRY32)
    out = []
    if k32.Thread32First(snap, ctypes.byref(te)):
        while True:
            if te.th32OwnerProcessID == pid:
                out.append(te.th32ThreadID)
            if not k32.Thread32Next(snap, ctypes.byref(te)):
                break
    k32.CloseHandle(snap)
    return out


def set_watchpoint(pid, sub_addrs):
    """DR0/DR1 = sub+0x18 (write, 8 bytes) for each sub candidate, on every thread."""
    dr7 = 0
    for i in range(min(2, len(sub_addrs))):
        dr7 |= (1 << (i * 2)) | (0x1 << (16 + i * 4)) | (0x3 << (18 + i * 4))
    n = 0
    for tid in threads_of(pid):
        ht = k32.OpenThread(THREAD_ACCESS, False, tid)
        if not ht:
            continue
        ctx = ctypes.create_string_buffer(1232)
        struct.pack_into("<I", ctx, 0x30, CONTEXT_DEBUG)
        k32.SuspendThread(ht)
        if k32.GetThreadContext(ht, ctx):
            for i in range(min(2, len(sub_addrs))):
                struct.pack_into("<Q", ctx, DR0_OFF + i * 8, sub_addrs[i] + 0x18)
            struct.pack_into("<Q", ctx, DR7_OFF, dr7)
            if k32.SetThreadContext(ht, ctx):
                n += 1
        k32.ResumeThread(ht)
        k32.CloseHandle(ht)
    return n


def func_of_module(mod, addr):
    if mod is None:
        return None
    try:
        ranges = getattr(mod, "functions", None)
        if ranges:
            for a, b in ranges:
                if a <= addr < b:
                    return a
    except Exception:
        pass
    return None


def main():
    si = STARTUPINFO()
    si.cb = ctypes.sizeof(STARTUPINFO)
    pi = PROCESS_INFORMATION()
    ok = k32.CreateProcessW(EXE, None, None, None, False,
                            DEBUG_ONLY_THIS_PROCESS, None, CWD,
                            ctypes.byref(si), ctypes.byref(pi))
    if not ok:
        print("CreateProcess failed err=%d" % k32.GetLastError())
        return 1
    pid = pi.dwProcessId
    print("pid=%d (debug)" % pid)
    name = (NAME_FMT % pid).encode()
    hmap = k32.CreateFileMappingA(INVALID_HANDLE_VALUE, None, 4, 0, BLOCK, name)
    p = k32.MapViewOfFile(hmap, FILE_MAP_ALL_ACCESS, 0, 0, BLOCK)
    ctypes.memmove(p, bytes(build_block()), BLOCK)
    k32.UnmapViewOfFile(p)

    hproc = None
    hmain = None
    mods = {}
    loaded = {}
    state_addr = None
    watch_done = False
    watch_addrs = []
    t0 = time.time()
    de = DEBUG_EVENT()

    def periodic():
        nonlocal state_addr, watch_done
        el = time.time() - t0
        if watch_done or not hproc or el < 0.85:
            return
        for base, size, nm, path in module_list(pid):
            if base not in mods:
                mods[base] = (nm, path, size)
        if not mods:
            return
        exe_base = None
        for base, (nm2, path2, size2) in mods.items():
            if nm2.lower().startswith("jx3client"):
                exe_base = base
                break
        if not exe_base:
            return
        # find the state sub-object: vtable == exe+0x953E50 and sub+8 -> state -> +0xE8 -> sub
        vt = exe_base + 0x953E50
        cands = scan_for(hproc.value, struct.pack("<Q", vt))
        subs = []
        for c in cands:
            st = read_u64(hproc.value, c + 8)
            if st and st + 0xE8 == c:   # the sub is embedded at state+0xE8
                subs.append(c)
        if not subs:
            r, sc = getattr(scan_for, "last", (0, 0))
            print("[%.2f] scan vt=0x%X: %d hits in %d regions / %.0f MB, no self-consistent sub"
                  % (el, vt, len(cands), r, sc / 1048576.0))
            for c in cands[:4]:
                st = read_u64(hproc.value, c + 8)
                back = read_u64(hproc.value, st + 0xE8) if st else None
                print("   hit 0x%X +8=0x%X back([+8]+0xE8)=%s" % (c, st or 0, hex(back) if back else "?"))
            return
        watch_done = True
        state_addr = read_u64(hproc.value, subs[0] + 8)
        watch_addrs[:] = subs
        n = set_watchpoint(pid, subs)
        print("[%.2f] subs=%s state=0x%X watch=%s on %d threads" % (
            el, ",".join("0x%X" % s for s in subs), state_addr or 0,
            ",".join("0x%X" % (s + 0x18) for s in subs), n))

    while True:
        if not k32.WaitForDebugEvent(ctypes.byref(de), 100):
            if time.time() - t0 > 8.0:
                print("timeout waiting for events")
                break
            periodic()
            continue
        code = de.dwDebugEventCode
        el = time.time() - t0
        if code == CREATE_PROCESS_DEBUG_EVENT:
            hproc = ctypes.c_void_p(struct.unpack_from("<Q", de.u, 8)[0])
            hmain = ctypes.c_void_p(struct.unpack_from("<Q", de.u, 16)[0])
            print("[%.2f] CREATE_PROCESS hproc=0x%X hthread=0x%X" % (el, hproc.value or 0, hmain.value or 0))
            k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
            continue
        if code == EXCEPTION_DEBUG_EVENT:
            rec = EXCEPTION_RECORD.from_buffer_copy(
                ctypes.string_at(ctypes.addressof(de.u), ctypes.sizeof(EXCEPTION_RECORD)))
            ec = rec.ExceptionCode
            if ec == EXCEPTION_SINGLE_STEP:
                ht = k32.OpenThread(THREAD_ACCESS, False, de.dwThreadId)
                ctx = ctypes.create_string_buffer(1232)
                struct.pack_into("<I", ctx, 0x30, CONTEXT_DEBUG)
                k32.GetThreadContext(ht, ctx)
                rip = struct.unpack_from("<Q", ctx, CTX_OFF["Rip"])[0]
                dr6 = struct.unpack_from("<Q", ctx, DR6_OFF)[0]
                print("[%.2f] SINGLE_STEP tid=%d dr6=0x%X RIP=0x%X" % (el, de.dwThreadId, dr6, rip))
                gate = None
                for i in range(4):
                    if dr6 & (1 << i):
                        print("   DR%d hit" % i)
                        if i < len(watch_addrs):
                            gate = read_u64(hproc.value, watch_addrs[i] + 0x18)
                print("   sub+0x18 now = 0x%X" % (gate or 0))
                before = read_mem(hproc.value, rip - 16, 16)
                print("   bytes before RIP: %s" % (before.hex() if before else "?"))
                for base, size, nm, path in module_list(pid):
                    if base <= rip < base + size:
                        print("   module=%s +0x%X" % (nm, rip - base))
                        break
                if not gate:
                    # a clear/init write (e.g. teardown) - keep watching for a real value
                    print("   zero write; continuing to watch")
                    k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
                    continue
                print("   NON-ZERO write 0x%X - creator found" % gate)
                k32.TerminateProcess(hproc, 1)
                return 0
            if ec == EXCEPTION_BREAKPOINT:
                k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
                continue
            # pass other exceptions to the process (its VEH/crash handler)
            k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_EXCEPTION_NOT_HANDLED)
            continue
        if code == LOAD_DLL_DEBUG_EVENT:
            hf = struct.unpack_from("<Q", de.u, 0)[0]
            if hf:
                k32.CloseHandle(ctypes.c_void_p(hf))
            k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
            continue
        if code == EXIT_PROCESS_DEBUG_EVENT:
            print("[%.2f] EXIT_PROCESS" % el)
            break
        # CREATE_THREAD/EXIT_THREAD/UNLOAD_DLL/OUTPUT_DEBUG_STRING/RIP
        k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
    print("no watchpoint hit")
    return 0


def sizeof_er():
    return ctypes.sizeof(EXCEPTION_RECORD)


if __name__ == "__main__":
    sys.exit(main())
