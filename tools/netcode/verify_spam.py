"""V2 P2: hammer Login_AccountVerify during the post-handshake window.

Watches the stub log for the client's handshake (RECV proto=4), then calls the
Login_AccountVerify binding (exe+0x20C930 -> 0x1401858E0 -> DoAccountVerifyRequest,
a thread-free call chain) every 30 ms for ~1.5 s so at least one call lands while the
gateway connection is still alive. No debugger, no Connect, no user input.

Usage: python verify_spam.py [--log C:\\jx3tmp\\gw_stdout.txt] [--ms 30] [--span 1.5]
"""
import ctypes
import ctypes.wintypes as w
import os
import sys
import time

k32 = ctypes.windll.kernel32
TH32CS_SNAPPROCESS = 0x2
TH32CS_SNAPMODULE = 0x8
RIGHTS = 0x438
VERIFY_RVA = 0x20C930


class PROCESSENTRY32(ctypes.Structure):
    _fields_ = [("dwSize", w.DWORD), ("cntUsage", w.DWORD), ("th32ProcessID", w.DWORD),
                ("th32DefaultHeapID", ctypes.POINTER(ctypes.c_ulong)), ("th32ModuleID", w.DWORD),
                ("cntThreads", w.DWORD), ("th32ParentProcessID", w.DWORD),
                ("pcPriClassBase", ctypes.c_long), ("dwFlags", w.DWORD),
                ("szExeFile", ctypes.c_char * 260)]


class MODULEENTRY32(ctypes.Structure):
    _fields_ = [("dwSize", w.DWORD), ("th32ModuleID", w.DWORD), ("th32ProcessID", w.DWORD),
                ("GlblcntUsage", w.DWORD), ("ProccntUsage", w.DWORD),
                ("modBaseAddr", ctypes.POINTER(ctypes.c_byte)), ("modBaseSize", w.DWORD),
                ("hModule", w.HMODULE), ("szModule", ctypes.c_char * 256),
                ("szExePath", ctypes.c_char * 260)]


def find_client():
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    pe = PROCESSENTRY32()
    pe.dwSize = ctypes.sizeof(PROCESSENTRY32)
    out = []
    if k32.Process32First(snap, ctypes.byref(pe)):
        while True:
            if pe.szExeFile.decode("gb18030", "replace").lower() == "jx3clientx64.exe":
                out.append(pe.th32ProcessID)
            if not k32.Process32Next(snap, ctypes.byref(pe)):
                break
    k32.CloseHandle(snap)
    return out


def module_base(pid, prefix):
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, pid)
    me = MODULEENTRY32()
    me.dwSize = ctypes.sizeof(MODULEENTRY32)
    base = None
    if k32.Module32First(snap, ctypes.byref(me)):
        while True:
            if me.szModule.decode("gb18030", "replace").lower().startswith(prefix):
                base = ctypes.cast(me.modBaseAddr, ctypes.c_void_p).value
                break
            if not k32.Module32Next(snap, ctypes.byref(me)):
                break
    k32.CloseHandle(snap)
    return base


def call_remote(hp, fn_va):
    k32.CreateRemoteThread.restype = w.HANDLE
    k32.CreateRemoteThread.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_size_t,
                                       ctypes.c_void_p, ctypes.c_void_p, w.DWORD,
                                       ctypes.POINTER(w.DWORD)]
    tid = w.DWORD()
    return k32.CreateRemoteThread(hp, None, 0, ctypes.c_void_p(fn_va), None, 0, ctypes.byref(tid))


def main():
    logpath = r"C:\jx3tmp\gw_stdout.txt"
    ms = 30
    span = 1.5
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--log" and i + 1 < len(args):
            logpath = args[i + 1]
        elif a == "--ms" and i + 1 < len(args):
            ms = int(args[i + 1])
        elif a == "--span" and i + 1 < len(args):
            span = float(args[i + 1])
    pids = find_client()
    if not pids:
        print("no client")
        return 1
    pid = pids[0]
    base = module_base(pid, "jx3client")
    hp = k32.OpenProcess(RIGHTS, False, pid)
    print("verify_spam: pid=%d base=0x%X" % (pid, base), flush=True)
    try:
        start = os.path.getsize(logpath)
    except OSError:
        start = 0
    t0 = time.time()
    while time.time() - t0 < 1800:
        try:
            with open(logpath, "rb") as f:
                f.seek(start)
                new = f.read().decode("utf-8", "replace")
        except OSError:
            new = ""
        if "RECV proto=4" in new:
            print("handshake seen -> spamming Login_AccountVerify", flush=True)
            end = time.time() + span
            n = 0
            while time.time() < end:
                th = call_remote(hp, base + VERIFY_RVA)
                n += 1
                time.sleep(ms / 1000.0)
            print("calls: %d" % n, flush=True)
            start = os.path.getsize(logpath) if os.path.exists(logpath) else start
            time.sleep(2.0)
        time.sleep(0.01)
    return 0


if __name__ == "__main__":
    sys.exit(main())
