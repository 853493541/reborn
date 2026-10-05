"""V2 P2: read-only gateway state watcher (no threads, no writes, no debugger).

Polls the gateway singleton in the running client every 50 ms and prints state changes:
state (+0x1250), cancel flag (+0x1268), connections (+0x28/+0x38), async handle
(+0x1258/+0x1260), WG/SM flags. Pure ReadProcessMemory - safe with the client's protection.

Usage: python watch_gateway.py [--seconds 120]
"""
import ctypes
import ctypes.wintypes as w
import struct
import sys
import time

k32 = ctypes.windll.kernel32
TH32CS_SNAPPROCESS = 0x2
TH32CS_SNAPMODULE = 0x8
GW_RVA = 0xA755F0


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


def rmem(hp, addr, n):
    buf = ctypes.create_string_buffer(n)
    got = ctypes.c_size_t()
    if k32.ReadProcessMemory(hp, ctypes.c_void_p(addr), buf, n, ctypes.byref(got)) and got.value == n:
        return buf.raw
    return None


def main():
    secs = 120
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--seconds" and i + 1 < len(args):
            secs = int(args[i + 1])
    pids = find_client()
    if not pids:
        print("no client running")
        return 1
    pid = pids[0]
    base = module_base(pid, "jx3client")
    hp = k32.OpenProcess(0x10, False, pid)  # PROCESS_VM_READ only
    print("watch pid=%d base=0x%X" % (pid, base), flush=True)
    gw = base + GW_RVA
    last = None
    t0 = time.time()
    it = 0
    while time.time() - t0 < secs:
        it += 1
        if it % 50 == 0 and not find_client():
            print("[%.2f] client gone" % (time.time() - t0), flush=True)
            break
        st = rmem(hp, gw + 0x1250, 4)
        f1 = rmem(hp, gw + 0x1268, 4)
        p28 = rmem(hp, gw + 0x28, 8)
        p38 = rmem(hp, gw + 0x38, 8)
        h1 = rmem(hp, gw + 0x1258, 8)
        h2 = rmem(hp, gw + 0x1260, 8)
        key = (st, f1, p28, p38, h1, h2)
        if key != last:
            last = key
            sv = struct.unpack("<I", st)[0] if st else None
            fv = struct.unpack("<I", f1)[0] if f1 else None
            p28v = struct.unpack("<Q", p28)[0] if p28 else 0
            p38v = struct.unpack("<Q", p38)[0] if p38 else 0
            h1v = struct.unpack("<Q", h1)[0] if h1 else 0
            h2v = struct.unpack("<Q", h2)[0] if h2 else 0
            print("[%.2f] state=%s f1268=%s p28=0x%X p38=0x%X h=0x%X/0x%X"
                  % (time.time() - t0, sv, fv, p28v or 0, p38v or 0, h1v or 0, h2v or 0), flush=True)
        time.sleep(0.02)
    return 0


if __name__ == "__main__":
    sys.exit(main())
