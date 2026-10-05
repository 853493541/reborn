"""V2 P2: drive Login_AccountVerify on the running client (no debugger).

Watches the gateway stub log for the client's handshake (RECV proto=4), then calls the
Login_AccountVerify binding (exe+0x20C930, which ignores its Lua-state argument and uses
the gateway singleton global) via CreateRemoteThread on our own emulator-launched client.
This drives the login past the stalled OnHandShakeSuccess UI path without any debugger.

Usage: python drive_verify.py [--log C:\\jx3tmp\\gw_stdout.txt] [--delay 4]
"""
import ctypes
import ctypes.wintypes as w
import os
import sys
import time

k32 = ctypes.windll.kernel32
TH32CS_SNAPPROCESS = 0x2
TH32CS_SNAPMODULE = 0x8
PROCESS_RIGHTS = 0x438  # CREATE_THREAD | VM_OP | VM_WRITE | VM_READ


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
            nm = pe.szExeFile.decode("gb18030", "replace").lower()
            if nm == "jx3clientx64.exe":
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
            nm = me.szModule.decode("gb18030", "replace").lower()
            if nm.startswith(prefix):
                base = ctypes.cast(me.modBaseAddr, ctypes.c_void_p).value
                break
            if not k32.Module32Next(snap, ctypes.byref(me)):
                break
    k32.CloseHandle(snap)
    return base


def main():
    logpath = r"C:\jx3tmp\gw_stdout.txt"
    delay = 4.0
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--log" and i + 1 < len(args):
            logpath = args[i + 1]
        elif a == "--delay" and i + 1 < len(args):
            delay = float(args[i + 1])
    print("drive_verify: watching %s (delay %.1fs)" % (logpath, delay), flush=True)
    seen_handshake = False
    called = False
    try:
        start_size = os.path.getsize(logpath)
    except OSError:
        start_size = 0
    t0 = time.time()
    while time.time() - t0 < 14400:
        pids = find_client()
        if pids and not seen_handshake:
            try:
                with open(logpath, "rb") as f:
                    f.seek(start_size)
                    txt = f.read().decode("utf-8", "replace")
            except OSError:
                txt = ""
            if "RECV proto=4" in txt:
                seen_handshake = True
                print("handshake seen; waiting %.1fs then calling Login_AccountVerify" % delay, flush=True)
                if delay > 0:
                    time.sleep(delay)
        if seen_handshake and not called and pids:
            pid = pids[0]
            base = module_base(pid, "jx3client")
            if base:
                hp = k32.OpenProcess(PROCESS_RIGHTS, False, pid)
                if hp:
                    k32.CreateRemoteThread.restype = w.HANDLE
                    k32.CreateRemoteThread.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_size_t,
                                                       ctypes.c_void_p, ctypes.c_void_p, w.DWORD,
                                                       ctypes.POINTER(w.DWORD)]
                    tid = w.DWORD()
                    th = k32.CreateRemoteThread(hp, None, 0,
                                                ctypes.c_void_p(base + 0x20C930),
                                                ctypes.c_void_p(0), 0, ctypes.byref(tid))
                    print("Login_AccountVerify called on pid=%d base=0x%X thread=%s" % (pid, base, th), flush=True)
                    called = True
                else:
                    print("OpenProcess failed err=%d" % k32.GetLastError(), flush=True)
            else:
                print("exe base not found", flush=True)
            if called:
                break
        time.sleep(0.05)
    print("drive_verify: done (handshake=%s called=%s)" % (seen_handshake, called), flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
