import ctypes
import ctypes.wintypes as w
import os
import struct
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, r"C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode")

from cipher_table import derive_table, encrypt_with_table
from unwind import PEModule, unwind_frame
import client_root

k32 = ctypes.windll.kernel32
CREATE_SUSPENDED = 0x4
FILE_MAP_ALL_ACCESS = 0xF001F
NAME_FMT = "400BBBA7-F29F-4357-9B07-%04X-D62109852BD6"
BLOCK = 0x275C
INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value
EXE = client_root.exe()
CWD = client_root.root()
TABLE = derive_table()
CONTEXT_FULL = 0x10000B
CTX_OFF = {"Rax": 0x78, "Rcx": 0x80, "Rdx": 0x88, "Rbx": 0x90, "Rsp": 0x98,
           "Rbp": 0xA0, "Rsi": 0xA8, "Rdi": 0xB0, "R8": 0xB8, "R9": 0xC0,
           "R10": 0xC8, "R11": 0xD0, "R12": 0xD8, "R13": 0xE0, "R14": 0xE8,
           "R15": 0xF0, "Rip": 0xF8}


class STARTUPINFO(ctypes.Structure):
    _fields_ = [("cb", w.DWORD), ("lpReserved", w.LPWSTR), ("lpDesktop", w.LPWSTR), ("lpTitle", w.LPWSTR),
                ("dwX", w.DWORD), ("dwY", w.DWORD), ("dwXSize", w.DWORD), ("dwYSize", w.DWORD),
                ("dwXCountChars", w.DWORD), ("dwYCountChars", w.DWORD), ("dwFillAttribute", w.DWORD),
                ("dwFlags", w.DWORD), ("wShowWindow", w.WORD), ("cbReserved2", w.WORD),
                ("lpReserved2", ctypes.c_void_p), ("hStdInput", w.HANDLE), ("hStdOutput", w.HANDLE), ("hStdError", w.HANDLE)]


class PROCESS_INFORMATION(ctypes.Structure):
    _fields_ = [("hProcess", w.HANDLE), ("hThread", w.HANDLE), ("dwProcessId", w.DWORD), ("dwThreadId", w.DWORD)]


k32.CreateProcessW.restype = w.BOOL
k32.CreateProcessW.argtypes = [w.LPCWSTR, w.LPWSTR, ctypes.c_void_p, ctypes.c_void_p, w.BOOL, w.DWORD,
                               ctypes.c_void_p, w.LPCWSTR, ctypes.POINTER(STARTUPINFO), ctypes.POINTER(PROCESS_INFORMATION)]
k32.CreateFileMappingA.restype = w.HANDLE
k32.CreateFileMappingA.argtypes = [ctypes.c_void_p, ctypes.c_void_p, w.DWORD, w.DWORD, w.DWORD, ctypes.c_char_p]
k32.MapViewOfFile.restype = ctypes.c_void_p
k32.MapViewOfFile.argtypes = [w.HANDLE, w.DWORD, w.DWORD, w.DWORD, ctypes.c_size_t]
k32.UnmapViewOfFile.restype = w.BOOL
k32.UnmapViewOfFile.argtypes = [ctypes.c_void_p]
k32.GetTickCount.restype = w.DWORD
k32.OpenProcess.restype = w.HANDLE
k32.OpenProcess.argtypes = [w.DWORD, w.BOOL, w.DWORD]
k32.SuspendThread.restype = w.DWORD
k32.OpenThread.restype = w.HANDLE
k32.OpenThread.argtypes = [w.DWORD, w.BOOL, w.DWORD]
k32.GetThreadContext.restype = w.BOOL
k32.GetThreadContext.argtypes = [w.HANDLE, ctypes.c_void_p]
k32.ReadProcessMemory.restype = w.BOOL
k32.ReadProcessMemory.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]


def build_block():
    blk = bytearray(BLOCK)
    ts = k32.GetTickCount() // 1000
    blk[2:6] = struct.pack("<I", ts)
    struct.pack_into("<I", blk, 8, 1)
    v0 = struct.unpack_from("<I", blk, 0)[0]
    v1 = struct.unpack_from("<I", blk, 4)[0]
    e0, e1 = encrypt_with_table(v0, v1, TABLE)
    struct.pack_into("<II", blk, 0, e0, e1)
    return blk


def threads_of(pid):
    TH32CS_SNAPTHREAD = 0x4

    class THREADENTRY32(ctypes.Structure):
        _fields_ = [("dwSize", w.DWORD), ("cntUsage", w.DWORD), ("th32ThreadID", w.DWORD), ("th32OwnerProcessID", w.DWORD),
                    ("tpBasePri", ctypes.c_long), ("tpDeltaPri", ctypes.c_long), ("dwFlags", w.DWORD)]

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


def module_list(pid):
    TH32CS_SNAPMODULE = 0x8
    TH32CS_SNAPMODULE32 = 0x10

    class MODULEENTRY32(ctypes.Structure):
        _fields_ = [("dwSize", w.DWORD), ("th32ModuleID", w.DWORD), ("th32ProcessID", w.DWORD), ("GlblcntUsage", w.DWORD),
                    ("ProccntUsage", w.DWORD), ("modBaseAddr", ctypes.POINTER(ctypes.c_byte)), ("modBaseSize", w.DWORD),
                    ("hModule", w.HMODULE), ("szModule", ctypes.c_char * 256), ("szExePath", ctypes.c_char * 260)]

    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, pid)
    me = MODULEENTRY32()
    me.dwSize = ctypes.sizeof(MODULEENTRY32)
    out = []
    if k32.Module32First(snap, ctypes.byref(me)):
        while True:
            base = ctypes.cast(me.modBaseAddr, ctypes.c_void_p).value or 0
            out.append((base, me.modBaseSize, me.szModule.decode("gb18030", "replace"), me.szExePath.decode("gb18030", "replace")))
            if not k32.Module32Next(snap, ctypes.byref(me)):
                break
    k32.CloseHandle(snap)
    return out


def main():
    si = STARTUPINFO()
    si.cb = ctypes.sizeof(STARTUPINFO)
    pi = PROCESS_INFORMATION()
    k32.CreateProcessW(EXE, None, None, None, False, CREATE_SUSPENDED, None, CWD, ctypes.byref(si), ctypes.byref(pi))
    pid = pi.dwProcessId
    name = (NAME_FMT % pid).encode()
    hmap = k32.CreateFileMappingA(INVALID_HANDLE_VALUE, None, 4, 0, BLOCK, name)
    p = k32.MapViewOfFile(hmap, FILE_MAP_ALL_ACCESS, 0, 0, BLOCK)
    ctypes.memmove(p, bytes(build_block()), BLOCK)
    k32.UnmapViewOfFile(p)
    k32.ResumeThread(pi.hThread)
    h = k32.OpenProcess(0x410, False, pid)
    t0 = time.time()
    print("pid=%d" % pid)

    def rpm(addr, size):
        buf = ctypes.create_string_buffer(size)
        n = ctypes.c_size_t()
        if k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, size, ctypes.byref(n)) and n.value == size:
            return buf.raw
        return None

    def q(addr):
        d = rpm(addr, 8)
        return struct.unpack("<Q", d)[0] if d else None

    def cstr(addr, n=200):
        d = rpm(addr, n)
        if not d:
            return None
        e = d.find(b"\x00")
        return d[:e if e != -1 else n].decode("ascii", "ignore")

    mods = {}
    loaded = {}
    exe_base = None

    def refresh():
        nonlocal exe_base
        for b, s, nm, path in module_list(pid):
            if b not in mods:
                mods[b] = (nm, path, s)
                if path and os.path.isfile(path):
                    try:
                        loaded[b] = PEModule(path, b)
                    except Exception:
                        pass
            if nm.lower().startswith("jx3client"):
                exe_base = b

    def mod_of(addr):
        best = None
        for b, (nm, path, s) in mods.items():
            if b <= addr < b + s:
                if best is None or b > best[0]:
                    best = (b, nm)
        return best

    def rtti_name(vt):
        if not vt:
            return None
        col = q(vt - 8)
        if not col:
            col = q(vt + 0x38)
        if not col:
            return None
        m = mod_of(col)
        if not m:
            return None
        cd = rpm(col, 24)
        if not cd:
            return None
        ptd = struct.unpack_from("<I", cd, 12)[0]
        return cstr(m[0] + ptd + 0x10)

    # find state via unwinding once
    state_addr = None
    ctx = ctypes.create_string_buffer(1232)
    refresh()
    while time.time() - t0 < 2.4:
        el = time.time() - t0
        if k32.WaitForSingleObject(pi.hProcess, 0) == 0:
            print("[%.2f] EXITED" % el)
            return
        if state_addr is None and el > 0.9:
            refresh()
            th = k32.OpenThread(0x0002 | 0x0008 | 0x0040, False, pi.dwThreadId)
            if th:
                k32.SuspendThread(th)
                struct.pack_into("<I", ctx, 0x30, CONTEXT_FULL)
                if k32.GetThreadContext(th, ctx):
                    regs = {n: struct.unpack_from("<Q", ctx, o)[0] for n, o in CTX_OFF.items()}
                    rip, rsp = regs["Rip"], regs["Rsp"]
                    by_base = sorted(loaded.items(), key=lambda kv: kv[0])
                    for i in range(50):
                        m = mod_of(rip)
                        if m is None:
                            break
                        if m[1].lower().startswith("jx3client") and 0xE11F0 <= rip - m[0] < 0xE1567:
                            state_addr = rsp + 0x78
                            print("[%.2f] state @0x%X" % (el, state_addr))
                            break
                        step, reason = unwind_frame(by_base, rpm, rip, rsp, regs)
                        if step is None:
                            break
                        rip, rsp, regs = step
                k32.ResumeThread(th)
                k32.CloseHandle(th)
        if state_addr and 1.88 < el < 2.20:
            # freeze all threads and walk the queue
            ths = []
            for tid in threads_of(pid):
                t = k32.OpenThread(0x0002 | 0x0008 | 0x0040, False, tid)
                if t:
                    k32.SuspendThread(t)
                    ths.append(t)
            sd = rpm(state_addr + 0xE8, 0x90)
            if sd:
                cur = struct.unpack_from("<Q", sd, 0x70)[0]
                nxt = struct.unpack_from("<Q", sd, 0x80)[0]
                flag61 = sd[0x61]
                tasks = []
                node = cur
                seen = set()
                while node and node not in seen and len(tasks) < 200:
                    seen.add(node)
                    task = q(node + 8)
                    ctxp = q(task + 8) if task else None
                    vt = q(ctxp) if ctxp else None
                    nm = rtti_name(vt) if vt else None
                    tasks.append((node, task, ctxp, nm))
                    node = q(node)
                print("[%.2f] done=%d queue cur=0x%X nxt=0x%X pending=%d" % (el, flag61, cur or 0, nxt or 0, len(tasks)))
                for i, (node, task, ctxp, nm) in enumerate(tasks[:60]):
                    print("    %2d node=0x%X task=0x%X ctx=0x%X %s" % (i, node, task or 0, ctxp or 0, nm))
            for t in ths:
                k32.ResumeThread(t)
                k32.CloseHandle(t)
            time.sleep(0.03)
        else:
            time.sleep(0.01)
    print("done")


if __name__ == "__main__":
    main()
