"""V2 P3: watch the game-login manager (exe+0xA8C4F0) through the login-key window.

Polls the manager fields set by 0x1401245A0: +0xe410 (game IP string), +0xe430 (port),
+0xe434 (roleID), +0xe438 (second string), +0xe3f8 (game transport). External reader
(0x438 rights); if the protection kills it, the log shows how far it got.

Usage: python watch_game_mgr.py [--secs 20]
"""
import ctypes
import ctypes.wintypes as w
import sys
import time

k32 = ctypes.windll.kernel32
TH32CS_SNAPPROCESS = 0x2
TH32CS_SNAPMODULE = 0x8
RIGHTS = 0x438
MGR_RVA = 0xA4C4F0


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


def read_mem(h, addr, size):
    buf = ctypes.create_string_buffer(size)
    n = ctypes.c_size_t()
    if k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, size, ctypes.byref(n)) and n.value == size:
        return buf.raw
    return None


def read_u32(h, addr):
    d = read_mem(h, addr, 4)
    import struct
    return struct.unpack("<I", d)[0] if d else None


def read_u64(h, addr):
    d = read_mem(h, addr, 8)
    import struct
    return struct.unpack("<Q", d)[0] if d else None


def main():
    secs = 20.0
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--secs" and i + 1 < len(args):
            secs = float(args[i + 1])
    pids = find_client()
    if not pids:
        print("no client")
        return 1
    pid = pids[0]
    base = module_base(pid, "jx3client")
    hp = k32.OpenProcess(RIGHTS, False, pid)
    mgr = base + MGR_RVA
    print("watching pid=%d base=0x%X mgr=0x%X" % (pid, base, mgr), flush=True)
    t0 = time.time()
    last = None
    while time.time() - t0 < secs:
        tr = read_u64(hp, mgr + 0xe3f8) or 0
        ip = read_mem(hp, mgr + 0xe410, 16)
        s2 = read_mem(hp, mgr + 0xe438, 16)
        port = read_u32(hp, mgr + 0xe430)
        role = read_u32(hp, mgr + 0xe434)
        inner = read_u64(hp, tr + 0x10) if tr else 0
        scst = read_u32(hp, inner + 0x3c) if inner else None
        rcst = read_u32(hp, inner + 0x40) if inner else None
        scb = read_u64(hp, inner + 0x48) if inner else None
        rcb = read_u64(hp, inner + 0x50) if inner else None
        key = (tr, port, role, ip, s2, inner, scst, rcst, scb, rcb)
        if key != last:
            last = key
            print("[%.2f] transport=0x%X port=%s role=%s ip=%r s2=%r"
                  % (time.time() - t0, tr, port, role, ip, s2), flush=True)
            print("[%.2f]   inner=0x%X scst=0x%X rcst=0x%X scb=0x%X rcb=0x%X"
                  % (time.time() - t0, inner, scst or 0, rcst or 0, scb or 0, rcb or 0), flush=True)
        time.sleep(0.02)
    print("done", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
