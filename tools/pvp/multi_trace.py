"""Attach to a live process, set up to 4 hardware EXECUTION breakpoints (DR0..DR3) by
module+offset, and report per-breakpoint hit counts + first-hit register context.

Read-only w.r.t. process memory (hardware breakpoints live in DR registers). NOTE (2026-10-08):
contrary to the earlier note in the docs, `DebugActiveProcess` does NOT kill the JX3 test client
(measured: a kernel32!GetTickCount hw BP fired 44k times and the client survived). Reuse this to
trace the client when a read-only probe cannot answer "is this function ever called?".

Usage:
  python multi_trace.py <pid> ModA+0xRVA [ModB+0xRVA ...] [--seconds N] [--inject-w]

  ModA is a loaded module name (JX3ClientX64.exe, JX3RepresentX64.dll, ...). --inject-w spawns a
  helper thread that focuses the KGWin32App window and SendInput's W repeatedly during the trace.
"""
from __future__ import annotations

import ctypes
import ctypes.wintypes as w
import struct
import sys
import time

sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
import mini_debugger as M  # noqa: E402

k32 = ctypes.windll.kernel32
TH32CS_SNAPMODULE = 0x8


class ME32(ctypes.Structure):
    _fields_ = [("dwSize", w.DWORD), ("th32ModuleID", w.DWORD), ("th32ProcessID", w.DWORD),
                ("GlblcntUsage", w.DWORD), ("ProccntUsage", w.DWORD),
                ("modBaseAddr", ctypes.c_void_p), ("modBaseSize", w.DWORD),
                ("hModule", w.HMODULE), ("szModule", ctypes.c_char * 256),
                ("szExePath", ctypes.c_char * 260)]


def mod_base(pid, name):
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, pid)
    me = ME32()
    me.dwSize = ctypes.sizeof(ME32)
    ok = k32.Module32First(snap, ctypes.byref(me))
    base = 0
    while ok:
        if me.szModule.decode("latin1").lower() == name.lower():
            base = me.modBaseAddr or 0
            break
        ok = k32.Module32Next(snap, ctypes.byref(me))
    k32.CloseHandle(snap)
    return base


def arm(tid, vas):
    ht = k32.OpenThread(0x0008 | 0x0010 | 0x0002 | 0x0040, False, tid)
    if not ht:
        return
    k32.SuspendThread(ht)
    ctx = M.CONTEXT()
    ctx.ContextFlags = M.CONTEXT_DEBUG_REGISTERS
    if k32.GetThreadContext(ht, ctypes.byref(ctx)):
        regs = [0, 0, 0, 0]
        dr7 = 0
        for idx, va in enumerate(vas[:4]):
            regs[idx] = va
            dr7 |= (0x1 << (idx * 2))  # Ln local-enable; rw/len = 0 (exec, 1 byte)
        ctx.Dr0, ctx.Dr1, ctx.Dr2, ctx.Dr3 = regs
        ctx.Dr7 = dr7
        ctx.ContextFlags = M.CONTEXT_DEBUG_REGISTERS
        k32.SetThreadContext(ht, ctypes.byref(ctx))
    k32.ResumeThread(ht)
    k32.CloseHandle(ht)


def start_injector():
    import threading
    VK_MENU = 0x12
    KEYEVENTF_KEYUP = 0x0002
    KEYEVENTF_SCANCODE = 0x0008
    INPUT_KEYBOARD = 1

    class KI(ctypes.Structure):
        _fields_ = [("wVk", w.WORD), ("wScan", w.WORD), ("dwFlags", w.DWORD),
                    ("time", w.DWORD), ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong))]

    class INP(ctypes.Structure):
        class _U(ctypes.Union):
            _fields_ = [("ki", KI), ("pad", ctypes.c_byte * 32)]
        _anonymous_ = ("u",)
        _fields_ = [("type", w.DWORD), ("u", _U)]

    def run():
        u32 = ctypes.windll.user32
        found = []

        @ctypes.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
        def cb(h, l):
            b = ctypes.create_unicode_buffer(64)
            u32.GetClassNameW(h, b, 64)
            if b.value == "KGWin32App" and u32.IsWindowVisible(h):
                found.append(h)
            return True
        u32.EnumWindows(cb, 0)
        if found:
            u32.AllowSetForegroundWindow(0xFFFFFFFF)
        fg_ok = 0
        sends = 0
        for _ in range(60):
            # re-assert focus every iteration (the shell/other windows steal it)
            if found:
                for _ in range(4):
                    if u32.GetForegroundWindow() == found[0]:
                        break
                    u32.keybd_event(VK_MENU, 0, 0, 0)
                    u32.keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0)
                    u32.SetForegroundWindow(found[0])
                    time.sleep(0.05)
            fg = (not found) or (u32.GetForegroundWindow() == found[0])
            if fg:
                fg_ok += 1
            inp = INP()
            inp.type = INPUT_KEYBOARD
            inp.ki = KI(0, 0x11, KEYEVENTF_SCANCODE, 0, None)
            u32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INP))
            sends += 1
            time.sleep(0.4)
            inp.ki = KI(0, 0x11, KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP, 0, None)
            u32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INP))
            sends += 1
            time.sleep(0.4)
        print("injector: sends=%d foreground_ok=%d interval" % (sends, fg_ok), flush=True)
    threading.Thread(target=run, daemon=True).start()


def main():
    pid = int(sys.argv[1])
    specs = []
    i = 2
    while i < len(sys.argv) and not sys.argv[i].startswith("--"):
        specs.append(sys.argv[i])
        i += 1
    seconds = 15
    if "--seconds" in sys.argv:
        seconds = int(sys.argv[sys.argv.index("--seconds") + 1])

    targets = []
    for s in specs:
        mod, off = s.split("+")
        va = mod_base(pid, mod) + int(off, 0)
        targets.append((mod, int(off, 0), va))
        print("target %s+0x%X = 0x%X" % (mod, int(off, 0), va))
    if not targets or len(targets) > 4:
        print("need 1..4 targets")
        return 1

    if "--inject-w" in sys.argv:
        start_injector()

    if not k32.DebugActiveProcess(pid):
        print("attach failed err=%d" % ctypes.get_last_error())
        return 1
    ev = M.DEBUG_EVENT()
    counts = {va: 0 for _, _, va in targets}
    first = {}
    vas = [va for _, _, va in targets]
    end = time.time() + seconds
    while time.time() < end:
        if not k32.WaitForDebugEvent(ctypes.byref(ev), 300):
            continue
        code = ev.dwDebugEventCode
        cont = M.DBG_CONTINUE
        if code in (M.CREATE_THREAD_DEBUG_EVENT, M.CREATE_PROCESS_DEBUG_EVENT):
            arm(ev.dwThreadId, vas)
        elif code == M.EXCEPTION_DEBUG_EVENT:
            ex_code = struct.unpack_from("<I", ev.u.pad, 0)[0]
            f = struct.unpack_from("<I", ev.u.pad, 4)[0]
            if ex_code == M.EXCEPTION_SINGLE_STEP and f == 0:
                ht = k32.OpenThread(0x0008 | 0x0010, False, ev.dwThreadId)
                ctx = M.CONTEXT()
                ctx.ContextFlags = M.CONTEXT_FULL
                if ht and k32.GetThreadContext(ht, ctypes.byref(ctx)):
                    for m, o, va in targets:
                        if ctx.Rip == va:
                            counts[va] += 1
                            if va not in first:
                                first[va] = "%s+0x%X rax=0x%X rcx=0x%X rdx=0x%X" % (
                                    m, o, ctx.Rax, ctx.Rcx, ctx.Rdx)
                    ctx.EFlags |= 0x10000
                    k32.SetThreadContext(ht, ctypes.byref(ctx))
                if ht:
                    k32.CloseHandle(ht)
            else:
                cont = M.DBG_EXCEPTION_NOT_HANDLED
        else:
            arm(ev.dwThreadId, vas)
        k32.ContinueDebugEvent(ev.dwProcessId, ev.dwThreadId, cont)
    k32.DebugActiveProcessStop(pid)
    for m, o, va in targets:
        print("HITS %s+0x%X: %d   %s" % (m, o, counts[va], first.get(va, "")))
    return 0


if __name__ == "__main__":
    sys.exit(main())
