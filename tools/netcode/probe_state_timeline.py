"""Timeline of the WinMain startup state in our own probe child.

Spawns JX3ClientX64.exe suspended, writes the launch block, resumes, finds the
WinMain frame once via .pdata unwinding, then samples the state object fields
(sub+0x18 status pointer, +0x60/+0x61 flags, +0x64 count, +0x70 current step)
every ~40 ms until the process exits.  Read-only; no injection.
"""
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
            out.append((base, me.modBaseSize, me.szModule.decode("gb18030", "replace"),
                        me.szExePath.decode("gb18030", "replace")))
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

    def read_mem(addr, size):
        buf = ctypes.create_string_buffer(size)
        n = ctypes.c_size_t()
        if k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, size, ctypes.byref(n)) and n.value == size:
            return buf.raw
        return None

    mods = {}
    loaded = {}

    def refresh():
        for base, size, nm, path in module_list(pid):
            if base not in mods:
                mods[base] = (nm, path, size)

    def get_module(rip):
        for base, (nm, path, size) in mods.items():
            if base <= rip < base + size:
                if base not in loaded:
                    loaded[base] = None
                    if path and os.path.isfile(path):
                        try:
                            loaded[base] = PEModule(path, base)
                        except Exception:
                            pass
                return base, loaded[base], nm
        return None

    ctx = ctypes.create_string_buffer(1232)
    state_addr = None
    globals_done = False
    while time.time() - t0 < 2.4:
        el = time.time() - t0
        if k32.WaitForSingleObject(pi.hProcess, 0) == 0:
            print("[%.2f] EXITED" % el)
            break
        if el > 0.5:
            refresh()
        if not mods:
            time.sleep(0.02)
            continue
        struct.pack_into("<I", ctx, 0x30, CONTEXT_FULL)
        k32.SuspendThread(pi.hThread)
        k32.GetThreadContext(pi.hThread, ctx)
        regs = {n: struct.unpack_from("<Q", ctx, o)[0] for n, o in CTX_OFF.items()}
        k32.ResumeThread(pi.hThread)
        rip, rsp = regs["Rip"], regs["Rsp"]
        if state_addr is None:
            by_base = sorted((b, m) for b, m in loaded.items() if m is not None)
            for i in range(40):
                m = get_module(rip)
                if m is None:
                    break
                base, mod, nm = m
                if nm.lower().startswith("jx3client") and 0xE11F0 <= rip - base < 0xE1567:
                    state_addr = rsp + 0x78
                    print("[%.2f] WinMain frame found, state @0x%X" % (el, state_addr))
                    break
                if mod is None:
                    break
                step, reason = unwind_frame(by_base, read_mem, rip, rsp, regs)
                if step is None:
                    break
                rip, rsp, regs = step
        if state_addr and not globals_done and el > 1.95:
            globals_done = True
            exe_base = None
            for base, (nm, path, size) in mods.items():
                if nm.lower().startswith("jx3client"):
                    exe_base = base
                    break
            if exe_base:
                gp = read_mem(exe_base + 0xA8C1F0, 8)
                if gp:
                    mapptr = struct.unpack("<Q", gp)[0]
                    print("[%.2f] registry map ptr [0xA8C1F0] = 0x%X" % (el, mapptr))
                    if mapptr:
                        md = read_mem(mapptr, 0x40)
                        if md:
                            print("   map bytes: " + " ".join("%016X" % struct.unpack_from("<Q", md, i)[0] for i in range(0, 0x40, 8)))
                for g, lbl in ((0xA8C218, "0xA8C218"), (0xA8C220, "0xA8C220"), (0xA8C1C8, "0xA8C1C8"), (0xA8C430, "vec_begin"), (0xA8C438, "vec_end")):
                    gv = read_mem(exe_base + g, 8)
                    if gv:
                        print("   [%s] = 0x%X" % (lbl, struct.unpack("<Q", gv)[0]))
        if state_addr:
            sd = read_mem(state_addr + 0xE8, 0x90)
            if sd:
                def q(o):
                    return struct.unpack_from("<Q", sd, o)[0]
                print("[%.2f] sub+0x18=0x%X +0x58=0x%X +0x60=%d +0x61=%d +0x64=%d +0x68=0x%X +0x70=0x%X +0x80=0x%X" % (
                    el, q(0x18), q(0x58), sd[0x60], sd[0x61], struct.unpack_from("<I", sd, 0x64)[0],
                    q(0x68), q(0x70), q(0x80)))
        time.sleep(0.04)


if __name__ == "__main__":
    main()

