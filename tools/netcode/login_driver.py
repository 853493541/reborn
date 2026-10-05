"""V2 P2: drive the gateway login on the running client (no debugger, no user).

Finds the emulator-launched client, writes the gateway address/account fields into the
gateway singleton (exe+0xA755F0), then calls the real C++ entry points via remote threads:
  Connect 0x140185ED0 (state 0 -> 1 + worker thread)         [trigger: connect]
  DoAccountVerifyRequest 0x1401865B0 (sends the 161B verify) [trigger: verify]
  DoLoginGameRequest is left to the UI.
Watches the stub log to report what arrived.

Usage: python login_driver.py [--verify-after 1.0] [--account admin]
"""
import ctypes
import ctypes.wintypes as w
import os
import struct
import sys
import time

k32 = ctypes.windll.kernel32
TH32CS_SNAPPROCESS = 0x2
TH32CS_SNAPMODULE = 0x8
RIGHTS = 0x438
GW_RVA = 0xA755F0
CONNECT_RVA = 0x185ED0
VERIFY_RVA = 0x1865B0


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


def wmem(hp, addr, data):
    n = ctypes.c_size_t()
    return k32.WriteProcessMemory(hp, ctypes.c_void_p(addr), data, len(data), ctypes.byref(n))


def rmem(hp, addr, n):
    buf = ctypes.create_string_buffer(n)
    got = ctypes.c_size_t()
    if k32.ReadProcessMemory(hp, ctypes.c_void_p(addr), buf, n, ctypes.byref(got)) and got.value == n:
        return buf.raw
    return None


def call_remote(hp, fn_va):
    k32.CreateRemoteThread.restype = w.HANDLE
    k32.CreateRemoteThread.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_size_t,
                                       ctypes.c_void_p, ctypes.c_void_p, w.DWORD,
                                       ctypes.POINTER(w.DWORD)]
    tid = w.DWORD()
    return k32.CreateRemoteThread(hp, None, 0, ctypes.c_void_p(fn_va), None, 0, ctypes.byref(tid))


def main():
    account = "admin"
    verify_after = 1.0
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--account" and i + 1 < len(args):
            account = args[i + 1]
        elif a == "--verify-after" and i + 1 < len(args):
            verify_after = float(args[i + 1])
    pids = find_client()
    if not pids:
        print("no client running")
        return 1
    pid = pids[0]
    base = module_base(pid, "jx3client")
    hp = k32.OpenProcess(RIGHTS, False, pid)
    print("client pid=%d base=0x%X hp=%s" % (pid, base, hp), flush=True)
    if not base or not hp:
        return 1
    gw = base + GW_RVA
    # address fields only (account/password are structured; never write them raw)
    wmem(hp, gw, b"127.0.0.1\x00" + b"\x00" * 22)
    wmem(hp, gw + 0x20, struct.pack("<H", 3724))
    wmem(hp, gw + 0x1250, struct.pack("<I", 0))
    wmem(hp, gw + 0x1268, struct.pack("<I", 0))
    print("fields written; calling Connect", flush=True)
    th = call_remote(hp, base + CONNECT_RVA)
    print("Connect thread=%s" % th, flush=True)
    t0 = time.time()
    verify_done = False
    while time.time() - t0 < 20:
        st = rmem(hp, gw + 0x1250, 4)
        f1 = rmem(hp, gw + 0x1268, 4)
        p28 = rmem(hp, gw + 0x28, 8)
        p38 = rmem(hp, gw + 0x38, 8)
        stv = struct.unpack("<I", st)[0] if st else None
        f1v = struct.unpack("<I", f1)[0] if f1 else None
        p28v = struct.unpack("<Q", p28)[0] if p28 else None
        p38v = struct.unpack("<Q", p38)[0] if p38 else None
        print("[%.2f] state=%s f1268=%s p28=0x%X p38=0x%X" % (time.time() - t0, stv, f1v, p28v or 0, p38v or 0), flush=True)
        if not verify_done and (time.time() - t0) > verify_after and p28v:
            verify_done = True
            th2 = call_remote(hp, base + VERIFY_RVA)
            print("[%.2f] DoAccountVerifyRequest called thread=%s" % (time.time() - t0, th2), flush=True)
        time.sleep(0.1)
    return 0


if __name__ == "__main__":
    sys.exit(main())
