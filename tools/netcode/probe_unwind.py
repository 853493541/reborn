"""Stack-walk our own probe child with real .pdata unwinding.

Spawns JX3ClientX64.exe suspended, writes the launch block, resumes, then at
target times suspends the main thread, reads its context and walks the stack
using each module's .pdata unwind info (read from disk; client memory for stack
values).  Prints every frame as module+offset so the exe call chain is visible.

Read-only: no injection, no writes into the client.  Our own child process.
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
from unwind import PEModule, REG_NAMES, unwind_frame

k32 = ctypes.windll.kernel32
CREATE_SUSPENDED = 0x4
FILE_MAP_ALL_ACCESS = 0xF001F
NAME_FMT = "400BBBA7-F29F-4357-9B07-%04X-D62109852BD6"
BLOCK = 0x275C
INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value
EXE = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\JX3ClientX64.exe"
CWD = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd"
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
            out.append((base, me.modBaseSize, me.szModule.decode("gb18030", "replace"), me.szExePath.decode("gb18030", "replace")))
            if not k32.Module32Next(snap, ctypes.byref(me)):
                break
    k32.CloseHandle(snap)
    return out


def main():
    targets = [float(x) for x in sys.argv[1:]] or [0.7, 1.3, 1.9]
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
    print("pid=%d targets=%s" % (pid, targets))
    mods = {}
    loaded = {}
    ctx = ctypes.create_string_buffer(1232)

    def read_mem(addr, size):
        buf = ctypes.create_string_buffer(size)
        n = ctypes.c_size_t()
        if k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, size, ctypes.byref(n)) and n.value == size:
            return buf.raw
        return None

    def refresh_modules():
        for base, size, nm, path in module_list(pid):
            if base not in mods:
                mods[base] = (nm, path, size)

    def load_module(base):
        if base in loaded:
            return loaded[base]
        nm, path, size = mods.get(base, (None, None, 0))
        if not path or not os.path.isfile(path):
            loaded[base] = None
            return None
        try:
            m = PEModule(path, base)
        except Exception:
            m = None
        loaded[base] = m
        return m

    def get_module(rip):
        for base, (nm, path, size) in mods.items():
            if base <= rip < base + size:
                return base, load_module(base), nm
        return None

    def get_module_refresh(rip):
        m = get_module(rip)
        if m is None:
            refresh_modules()
            m = get_module(rip)
        return m

    for target in targets:
        while time.time() - t0 < target:
            if k32.WaitForSingleObject(pi.hProcess, 0) == 0:
                print("[%.2f] EXITED" % (time.time() - t0))
                return
            time.sleep(0.01)
        fresh = module_list(pid)
        if fresh:
            mods = {base: (nm, path, size) for base, size, nm, path in fresh}
        struct.pack_into("<I", ctx, 0x30, CONTEXT_FULL)
        k32.SuspendThread(pi.hThread)
        k32.GetThreadContext(pi.hThread, ctx)
        regs = {n: struct.unpack_from("<Q", ctx, o)[0] for n, o in CTX_OFF.items()}
        k32.ResumeThread(pi.hThread)
        rip = regs["Rip"]
        rsp = regs["Rsp"]
        print("== t=%.2f frames (RIP=0x%X RSP=0x%X) ==" % (time.time() - t0, rip, rsp))
        raw = read_mem(rsp, 32)
        if raw:
            print("   [rsp]: " + " ".join("0x%X" % struct.unpack_from("<Q", raw, i * 8)[0] for i in range(4)))
        by_base = sorted(loaded.items(), key=lambda kv: kv[0])
        for i in range(60):
            m = get_module_refresh(rip)
            if m is None:
                print("  %2d 0x%X  (not in any loaded module - chain broken)" % (i, rip))
                break
            base, mod, nm = m
            print("  %2d 0x%X  %s+0x%X" % (i, rip, nm, rip - base))
            if nm.lower().startswith("jx3client") and 0xE11F0 <= (rip - base) < 0xE1567:
                state_addr = rsp + 0x78
                sd = read_mem(state_addr, 0x1B0)
                if sd:
                    sub = 0xE8

                    def q(o):
                        return struct.unpack_from("<Q", sd, sub + o)[0]
                    print("       WinMain state @0x%X sub@+0x%X: vt=0x%X [0x18]=0x%X [0x58]=0x%X [0x60]=%d [0x61]=%d [0x64]=%d [0x68]=0x%X [0x70]=0x%X [0x80]=0x%X" % (
                        state_addr, sub, q(0), q(0x18), q(0x58), sd[sub + 0x60], sd[sub + 0x61],
                        struct.unpack_from("<I", sd, sub + 0x64)[0], q(0x68), q(0x70), q(0x80)))
                    obj18 = q(0x18)
                    if obj18:
                        vt = read_mem(obj18, 8)
                        if vt:
                            vtable = struct.unpack("<Q", vt)[0]
                            print("       state[0x18] obj @0x%X vtable=0x%X" % (obj18, vtable))
                            fns = read_mem(vtable, 0x28)
                            if fns:
                                for o in range(0, 0x28, 8):
                                    f = struct.unpack_from("<Q", fns, o)[0]
                                    mm = get_module_refresh(f) if f > 0x10000 else None
                                    if mm:
                                        print("          vtable+0x%X -> %s+0x%X" % (o, mm[2], f - mm[0]))
                                    else:
                                        print("          vtable+0x%X -> 0x%X" % (o, f))
            if rsp < 0x10000 or rsp > 0x7FFFFFFFFFFF:
                break
            by_base = sorted(loaded.items(), key=lambda kv: kv[0])
            step, reason = unwind_frame(by_base, read_mem, rip, rsp, regs)
            if step is None:
                print("       unwind stopped: %s (rsp=0x%X)" % (reason, rsp))
                break
            new_rip, new_rsp, regs = step
            if new_rip == 0 or get_module_refresh(new_rip) is None:
                print("       next rip 0x%X not in any module - chain broken" % new_rip)
                break
            rip, rsp = new_rip, new_rsp
        print("")


if __name__ == "__main__":
    main()

