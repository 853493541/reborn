"""V2 P1.2: list every module whose Initialize handler fails, via a software breakpoint.

Spawns JX3ClientX64.exe under DEBUG_ONLY_THIS_PROCESS, creates the launch block, sets an
INT3 at the module-Initialize failure branch (exe+0xA38FF, reached only when the handler
returned 0), and on each hit resolves the handler context's RTTI name (the module) and
prints it. Read-only observation of our own probe child.
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
    STARTUPINFO, PROCESS_INFORMATION, build_block, module_list,
)

DEBUG_ONLY_THIS_PROCESS = 0x2
DBG_CONTINUE = 0x00010002
DBG_EXCEPTION_NOT_HANDLED = 0x80010001
EXCEPTION_DEBUG_EVENT = 1
CREATE_PROCESS_DEBUG_EVENT = 3
EXIT_PROCESS_DEBUG_EVENT = 5
LOAD_DLL_DEBUG_EVENT = 6
EXCEPTION_BREAKPOINT = 0x80000003
EXCEPTION_SINGLE_STEP = 0x80000004
FAIL_BRANCH = 0xA38FF          # mov qword [rsp+0x20], r9 in the failure path
SUCCESS_BRANCH = 0xA3975       # mov r12b, 1 (optional, unused)
STEP_PROC = 0xA3FF0            # module-step process: returns 0 when low byte of rdx == 0
CTX_FULL = 0x10000B


class DEBUG_EVENT(ctypes.Structure):
    _fields_ = [("dwDebugEventCode", w.DWORD), ("dwProcessId", w.DWORD),
                ("dwThreadId", w.DWORD), ("__pad", w.DWORD), ("u", ctypes.c_byte * 160)]


k32.WaitForDebugEvent.restype = w.BOOL
k32.WaitForDebugEvent.argtypes = [ctypes.POINTER(DEBUG_EVENT), w.DWORD]
k32.ContinueDebugEvent.restype = w.BOOL
k32.ContinueDebugEvent.argtypes = [w.DWORD, w.DWORD, w.DWORD]
k32.GetThreadContext.restype = w.BOOL
k32.GetThreadContext.argtypes = [w.HANDLE, ctypes.c_void_p]
k32.SetThreadContext.restype = w.BOOL
k32.SetThreadContext.argtypes = [w.HANDLE, ctypes.c_void_p]
k32.OpenThread.restype = w.HANDLE
k32.OpenThread.argtypes = [w.DWORD, w.BOOL, w.DWORD]

THREAD_ACCESS = 0x0002 | 0x0004 | 0x0008 | 0x0010
R13_OFF = 0xE0


def read_mem(h, addr, size):
    buf = ctypes.create_string_buffer(size)
    n = ctypes.c_size_t()
    if k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, size, ctypes.byref(n)) and n.value == size:
        return buf.raw
    return None


def read_u64(h, addr):
    d = read_mem(h, addr, 8)
    return struct.unpack("<Q", d)[0] if d else None


def rtti_name(h, exe_base, obj):
    vt = read_u64(h, obj)
    if not vt:
        return None
    col = read_u64(h, vt - 8)
    if not col:
        return None
    raw = read_mem(h, col + 0xC, 4)
    if not raw:
        return None
    td = exe_base + struct.unpack("<I", raw)[0]
    nb = read_mem(h, td + 0x10, 128)
    if not nb:
        return None
    return nb.split(b"\x00")[0].decode("latin-1", "replace")


def main():
    si = STARTUPINFO()
    si.cb = ctypes.sizeof(STARTUPINFO)
    pi = PROCESS_INFORMATION()
    ok = k32.CreateProcessW(EXE, None, None, None, False, DEBUG_ONLY_THIS_PROCESS, None, CWD,
                            ctypes.byref(si), ctypes.byref(pi))
    if not ok:
        print("CreateProcess failed err=%d" % k32.GetLastError())
        return 1
    pid = pi.dwProcessId
    print("pid=%d (debug)" % pid, flush=True)
    name = (NAME_FMT % pid).encode()
    hmap = k32.CreateFileMappingA(INVALID_HANDLE_VALUE, None, 4, 0, BLOCK, name)
    p = k32.MapViewOfFile(hmap, FILE_MAP_ALL_ACCESS, 0, 0, BLOCK)
    ctypes.memmove(p, bytes(build_block()), BLOCK)
    k32.UnmapViewOfFile(p)

    hproc = None
    exe_base = None
    armed = False
    original = None
    orig_bytes = {}
    hits = []
    t0 = time.time()
    de = DEBUG_EVENT()
    ctx = ctypes.create_string_buffer(1232)
    while True:
        got = k32.WaitForDebugEvent(ctypes.byref(de), 100)
        el = time.time() - t0
        if el > 12.0:
            print("timeout", flush=True)
            break
        if not got:
            if not armed and hproc and exe_base:
                # arm both breakpoints once the exe is loaded
                original = read_mem(hproc.value, exe_base + FAIL_BRANCH, 1)
                if original:
                    write = ctypes.c_size_t()
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(exe_base + FAIL_BRANCH),
                                           b"\xCC", 1, ctypes.byref(write))
                    orig_bytes[FAIL_BRANCH] = original
                    armed = True
                    print("[%.2f] failure breakpoint armed" % el, flush=True)
            if armed and hproc and exe_base and STEP_PROC not in orig_bytes and el > 2.5:
                orig_step = read_mem(hproc.value, exe_base + STEP_PROC, 1)
                if orig_step:
                    write = ctypes.c_size_t()
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(exe_base + STEP_PROC),
                                           b"\xCC", 1, ctypes.byref(write))
                    orig_bytes[STEP_PROC] = orig_step
                    print("[%.2f] step breakpoint armed late" % el, flush=True)
            continue
        code = de.dwDebugEventCode
        if code == CREATE_PROCESS_DEBUG_EVENT:
            hproc = ctypes.c_void_p(struct.unpack_from("<Q", de.u, 8)[0])
            print("[%.2f] CREATE_PROCESS" % el, flush=True)
            k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
            continue
        if code == LOAD_DLL_DEBUG_EVENT:
            hf = struct.unpack_from("<Q", de.u, 0)[0]
            if hf:
                k32.CloseHandle(ctypes.c_void_p(hf))
            if exe_base is None and hproc:
                for base, size, nm, path in module_list(pid):
                    if nm.lower().startswith("jx3client"):
                        exe_base = base
                        break
            k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
            continue
        if code == EXCEPTION_DEBUG_EVENT:
            ec = struct.unpack_from("<I", de.u, 0)[0]
            addr = struct.unpack_from("<Q", de.u, 16)[0]
            if ec == EXCEPTION_BREAKPOINT and exe_base and addr == exe_base + STEP_PROC:
                ht = k32.OpenThread(THREAD_ACCESS, False, de.dwThreadId)
                struct.pack_into("<I", ctx, 0x30, CTX_FULL)
                k32.GetThreadContext(ht, ctx)
                rcx = struct.unpack_from("<Q", ctx, 0x80)[0]
                rdx = struct.unpack_from("<Q", ctx, 0x88)[0]
                rsp = struct.unpack_from("<Q", ctx, 0x98)[0]
                ret = read_u64(hproc.value, rsp) if rsp else None
                hctx = read_u64(hproc.value, rcx + 0x10) if rcx else None
                nm = rtti_name(hproc.value, exe_base, hctx) if hctx else None
                if nm and "LaunchUpdater" in nm:
                    print("[%.2f] STEP_PROC %s rcx=0x%X rdx=0x%X dl=0x%X ret=0x%X (exe+0x%X)" % (
                        el, nm, rcx, rdx, rdx & 0xFF, ret or 0,
                        (ret - exe_base) if ret and ret > exe_base else 0), flush=True)
                # restore, single-step, re-arm
                write = ctypes.c_size_t()
                k32.WriteProcessMemory(hproc, ctypes.c_void_p(exe_base + STEP_PROC),
                                       orig_bytes[STEP_PROC], 1, ctypes.byref(write))
                flags = struct.unpack_from("<I", ctx, 0x44)[0]
                struct.pack_into("<I", ctx, 0x44, flags | 0x100)
                k32.SetThreadContext(ht, ctx)
                k32.CloseHandle(ht)
                k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
                continue
            if ec == EXCEPTION_BREAKPOINT and exe_base and addr == exe_base + FAIL_BRANCH:
                ht = k32.OpenThread(THREAD_ACCESS, False, de.dwThreadId)
                struct.pack_into("<I", ctx, 0x30, CTX_FULL)
                k32.GetThreadContext(ht, ctx)
                r13 = struct.unpack_from("<Q", ctx, R13_OFF)[0]
                # lambda: rcx = node+8 -> r13; [r13] = handler ctx (module); [r13+8] = event id
                hctx = read_u64(hproc.value, r13) if r13 else None
                eid_b = read_mem(hproc.value, r13 + 8, 4) if r13 else None
                eid = struct.unpack("<I", eid_b)[0] if eid_b else -1
                nm = rtti_name(hproc.value, exe_base, hctx) if hctx else None
                fn = None
                vt = read_u64(hproc.value, hctx) if hctx else None
                if vt:
                    fnb = read_mem(hproc.value, vt + 0x20, 8)
                    if fnb:
                        fn = struct.unpack("<Q", fnb)[0]
                key = (nm, eid, fn)
                if key not in hits:
                    hits.append(key)
                    print("[%.2f] MODULE INIT FAIL: %s event=%d handler=0x%X (exe+0x%X)" % (
                        el, nm, eid, fn or 0, (fn - exe_base) if fn and fn > exe_base else 0), flush=True)
                # restore, single-step, re-arm
                write = ctypes.c_size_t()
                k32.WriteProcessMemory(hproc, ctypes.c_void_p(exe_base + FAIL_BRANCH),
                                       original, 1, ctypes.byref(write))
                flags = struct.unpack_from("<I", ctx, 0x44)[0]
                struct.pack_into("<I", ctx, 0x44, flags | 0x100)
                k32.SetThreadContext(ht, ctx)
                k32.CloseHandle(ht)
                k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
                continue
            if ec == EXCEPTION_SINGLE_STEP and armed and exe_base:
                # re-arm after stepping over the restored byte
                ht = k32.OpenThread(THREAD_ACCESS, False, de.dwThreadId)
                struct.pack_into("<I", ctx, 0x30, CTX_FULL)
                k32.GetThreadContext(ht, ctx)
                rip = struct.unpack_from("<Q", ctx, 0xF8)[0]
                write = ctypes.c_size_t()
                if rip == exe_base + FAIL_BRANCH + 1:
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(exe_base + FAIL_BRANCH),
                                           b"\xCC", 1, ctypes.byref(write))
                elif rip == exe_base + STEP_PROC + 1:
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(exe_base + STEP_PROC),
                                           b"\xCC", 1, ctypes.byref(write))
                k32.CloseHandle(ht)
                k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
                continue
            if ec == EXCEPTION_BREAKPOINT:
                k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
                continue
            k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_EXCEPTION_NOT_HANDLED)
            continue
        if code == EXIT_PROCESS_DEBUG_EVENT:
            print("[%.2f] EXIT_PROCESS" % el, flush=True)
            break
        k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
    print("failing modules: %s" % (hits if hits else "none"), flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
