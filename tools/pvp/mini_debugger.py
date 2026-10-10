"""Minimal Windows debugger: attach read-only, set a HARDWARE execution breakpoint (DR0) on
each thread for a target address, log the register context when it hits, then detach.

No process memory is modified (hardware breakpoints live in DR registers). Read-only intent:
observe which code runs and with what register args.

Usage: python mini_debugger.py <pid> <hex_bp_addr> [--seconds N]
"""
from __future__ import annotations

import ctypes
import ctypes.wintypes as w
import struct
import sys
import time

k32 = ctypes.windll.kernel32

DBG_CONTINUE = 0x00010002
DBG_EXCEPTION_NOT_HANDLED = 0x80010001
EXCEPTION_DEBUG_EVENT = 1
CREATE_THREAD_DEBUG_EVENT = 2
CREATE_PROCESS_DEBUG_EVENT = 3
EXIT_THREAD_DEBUG_EVENT = 4
EXIT_PROCESS_DEBUG_EVENT = 5
LOAD_DLL_DEBUG_EVENT = 6
OUTPUT_DEBUG_STRING_EVENT = 8
EXCEPTION_SINGLE_STEP = 0x80000004
EXCEPTION_BREAKPOINT = 0x80000003

THREAD_GET_CONTEXT = 0x0008
THREAD_SET_CONTEXT = 0x0010
THREAD_SUSPEND_RESUME = 0x0002
THREAD_QUERY_INFORMATION = 0x0040

CONTEXT_AMD64 = 0x00100000
CONTEXT_DEBUG_REGISTERS = CONTEXT_AMD64 | 0x0010
CONTEXT_FULL = CONTEXT_AMD64 | 0x0000000B


class CONTEXT(ctypes.Structure):
    _fields_ = [("P1Home", ctypes.c_uint64), ("P2Home", ctypes.c_uint64), ("P3Home", ctypes.c_uint64),
                ("P4Home", ctypes.c_uint64), ("P5Home", ctypes.c_uint64), ("P6Home", ctypes.c_uint64),
                ("ContextFlags", w.DWORD), ("MxCsr", w.DWORD),
                ("SegCs", w.WORD), ("SegDs", w.WORD), ("SegEs", w.WORD), ("SegFs", w.WORD),
                ("SegGs", w.WORD), ("SegSs", w.WORD), ("EFlags", w.DWORD),
                ("Dr0", ctypes.c_uint64), ("Dr1", ctypes.c_uint64), ("Dr2", ctypes.c_uint64),
                ("Dr3", ctypes.c_uint64), ("Dr6", ctypes.c_uint64), ("Dr7", ctypes.c_uint64),
                ("Rax", ctypes.c_uint64), ("Rcx", ctypes.c_uint64), ("Rdx", ctypes.c_uint64),
                ("Rbx", ctypes.c_uint64), ("Rsp", ctypes.c_uint64), ("Rbp", ctypes.c_uint64),
                ("Rsi", ctypes.c_uint64), ("Rdi", ctypes.c_uint64), ("R8", ctypes.c_uint64),
                ("R9", ctypes.c_uint64), ("R10", ctypes.c_uint64), ("R11", ctypes.c_uint64),
                ("R12", ctypes.c_uint64), ("R13", ctypes.c_uint64), ("R14", ctypes.c_uint64),
                ("R15", ctypes.c_uint64), ("Rip", ctypes.c_uint64),
                # remaining space so sizeof(CONTEXT) == 0x4D0 (required by Get/SetThreadContext;
                # a short buffer makes SetThreadContext fail silently)
                ("_pad", ctypes.c_byte * 0x3D0)]


class DEBUG_EVENT(ctypes.Structure):
    class _U(ctypes.Union):
        # _a forces 8-byte alignment so the union lands at offset 16 (matching the
        # Win32 DEBUG_EVENT); without it ctypes puts it at 12 and every field is off by 4.
        _fields_ = [("_a", ctypes.c_uint64), ("pad", ctypes.c_byte * 160)]

    _fields_ = [("dwDebugEventCode", w.DWORD), ("dwProcessId", w.DWORD),
                ("dwThreadId", w.DWORD), ("u", _U)]


def set_hw_bp(tid, addr, verbose=False):
    if verbose:
        print("CREATE_THREAD tid=%d -> arm bp 0x%X" % (tid, addr))
    ht = k32.OpenThread(THREAD_GET_CONTEXT | THREAD_SET_CONTEXT | THREAD_SUSPEND_RESUME
                        | THREAD_QUERY_INFORMATION, False, tid)
    if not ht:
        if verbose:
            print("  OpenThread failed tid=%d err=%d" % (tid, ctypes.get_last_error()))
        return False
    k32.SuspendThread(ht)
    ctx = CONTEXT()
    ctx.ContextFlags = CONTEXT_DEBUG_REGISTERS
    ok = False
    if k32.GetThreadContext(ht, ctypes.byref(ctx)):
        ctx.Dr0 = addr
        ctx.Dr7 = (ctx.Dr7 | 0x1) & ~0x30000  # enable L0, exec, len=1
        ctx.ContextFlags = CONTEXT_DEBUG_REGISTERS
        if k32.SetThreadContext(ht, ctypes.byref(ctx)):
            ok = True
        elif verbose:
            print("  SetThreadContext failed tid=%d err=%d" % (tid, ctypes.get_last_error()))
    elif verbose:
        print("  GetThreadContext failed tid=%d err=%d" % (tid, ctypes.get_last_error()))
    k32.ResumeThread(ht)
    k32.CloseHandle(ht)
    return ok


def clear_hw_bp(tid):
    ht = k32.OpenThread(THREAD_GET_CONTEXT | THREAD_SET_CONTEXT | THREAD_SUSPEND_RESUME, False, tid)
    if not ht:
        return
    k32.SuspendThread(ht)
    ctx = CONTEXT()
    ctx.ContextFlags = CONTEXT_DEBUG_REGISTERS
    if k32.GetThreadContext(ht, ctypes.byref(ctx)):
        ctx.Dr0 = 0
        ctx.Dr7 = ctx.Dr7 & ~0x1
        ctx.ContextFlags = CONTEXT_DEBUG_REGISTERS
        k32.SetThreadContext(ht, ctypes.byref(ctx))
    k32.ResumeThread(ht)
    k32.CloseHandle(ht)


def main():
    pid = int(sys.argv[1])
    bp = int(sys.argv[2], 0)
    seconds = 120
    if "--seconds" in sys.argv:
        seconds = int(sys.argv[sys.argv.index("--seconds") + 1])
    if not k32.DebugActiveProcess(pid):
        print("DebugActiveProcess failed err=%d" % ctypes.get_last_error())
        return 1
    print("attached pid=%d bp=0x%X" % (pid, bp))
    ev = DEBUG_EVENT()
    end = time.time() + seconds
    hits = 0
    armed = 0
    while time.time() < end:
        if not k32.WaitForDebugEvent(ctypes.byref(ev), 500):
            continue
        code = ev.dwDebugEventCode
        cont = DBG_CONTINUE
        if code == CREATE_THREAD_DEBUG_EVENT:
            if set_hw_bp(ev.dwThreadId, bp, verbose=True):
                armed += 1
        elif code == CREATE_PROCESS_DEBUG_EVENT:
            if set_hw_bp(ev.dwThreadId, bp, verbose=True):
                armed += 1
        elif code == EXCEPTION_DEBUG_EVENT:
            u = ev.u.pad
            ex_code = struct.unpack_from("<I", u, 0)[0]
            first = struct.unpack_from("<I", u, 4)[0]
            ex_addr = struct.unpack_from("<Q", u, 16)[0]
            print("EXCEPTION tid=%d code=0x%08X flags=0x%X addr=0x%X" %
                  (ev.dwThreadId, ex_code, first, ex_addr))
            if ex_code == EXCEPTION_SINGLE_STEP and first == 0:
                # read the thread context (need SET too: we set RF so the same instruction
                # runs without re-triggering the HW BP, letting us keep hitting it)
                ht = k32.OpenThread(THREAD_GET_CONTEXT | THREAD_SET_CONTEXT, False, ev.dwThreadId)
                ctx = CONTEXT()
                ctx.ContextFlags = CONTEXT_FULL
                if ht and k32.GetThreadContext(ht, ctypes.byref(ctx)):
                    print("HIT tid=%d rip=0x%X rax=0x%X rcx=0x%X rdx=0x%X r8=0x%X" %
                          (ev.dwThreadId, ctx.Rip, ctx.Rax, ctx.Rcx, ctx.Rdx, ctx.R8))
                    # if rcx/rdx look like pointers, try to read a C-string (LoadLibrary arg etc.)
                    hp = k32.OpenProcess(0x410, False, ev.dwProcessId)
                    for reg, val in (("rcx", ctx.Rcx), ("rdx", ctx.Rdx), ("r8", ctx.R8)):
                        if not (0x10000 < val < 0x7FFFFFFFFFFF):
                            continue
                        buf = ctypes.create_string_buffer(260)
                        got = ctypes.c_size_t(0)
                        if k32.ReadProcessMemory(hp, ctypes.c_void_p(val), buf, 260, ctypes.byref(got)):
                            raw = buf.raw[:got.value].split(b"\x00")[0]
                            if raw and all(32 <= c < 127 for c in raw[:min(len(raw), 200)]):
                                print("   %s -> %r" % (reg, raw.decode("latin1")))
                    if hp:
                        k32.CloseHandle(hp)
                    ctx.EFlags |= 0x10000  # RF: let the instruction run without re-triggering
                    ctx.ContextFlags = CONTEXT_FULL
                    k32.SetThreadContext(ht, ctypes.byref(ctx))
                    hits += 1
                if ht:
                    k32.CloseHandle(ht)
            else:
                cont = DBG_EXCEPTION_NOT_HANDLED
        k32.ContinueDebugEvent(ev.dwProcessId, ev.dwThreadId, cont)
    k32.DebugActiveProcessStop(pid)
    print("detached; armed=%d threads hits=%d" % (armed, hits))
    return 0


if __name__ == "__main__":
    sys.exit(main())
