import ctypes
import ctypes.wintypes as w
import os
import struct
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, r"C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode")
from cipher_table import derive_table, encrypt_with_table
import client_root

k32 = ctypes.windll.kernel32
ntdll = ctypes.windll.ntdll
CREATE_SUSPENDED = 0x4
FILE_MAP_ALL_ACCESS = 0xF001F
NAME_FMT = "400BBBA7-F29F-4357-9B07-%04X-D62109852BD6"
BLOCK = 0x275C
INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value
EXE = client_root.exe()
CWD = client_root.root()
TABLE = derive_table()
CONTEXT_FULL = 0x10000B


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
k32.GetModuleHandleW.restype = w.HMODULE
k32.GetModuleHandleW.argtypes = [w.LPCWSTR]
k32.GetProcAddress.restype = ctypes.c_void_p
k32.GetProcAddress.argtypes = [w.HMODULE, ctypes.c_char_p]
k32.ReadProcessMemory.restype = w.BOOL
k32.ReadProcessMemory.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
ntdll.NtQueryObject.restype = ctypes.c_long
k32.DuplicateHandle.restype = w.BOOL


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


def rpm(h, addr, size):
    buf = ctypes.create_string_buffer(size)
    n = ctypes.c_size_t()
    if k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, size, ctypes.byref(n)):
        return buf.raw[:n.value]
    return None


def rpm_tol(h, addr, size, page=0x1000):
    out = bytearray()
    pos = addr
    end = addr + size
    while pos < end:
        chunk = min(page, end - pos)
        b = rpm(h, pos, chunk)
        out += b if b is not None else b"\x00" * chunk
        pos += chunk
    return bytes(out)


def obj_info(pid, hval):
    hproc = k32.OpenProcess(0x40, False, pid)
    if not hproc:
        return None
    dup = w.HANDLE()
    if not k32.DuplicateHandle(hproc, w.HANDLE(hval), k32.GetCurrentProcess(), ctypes.byref(dup), 0, False, 0x2):
        k32.CloseHandle(hproc)
        return None
    name = ""
    typ = ""
    buf = ctypes.create_string_buffer(0x2000)
    rl = ctypes.c_ulong()
    if (ntdll.NtQueryObject(dup, 1, buf, 0x2000, ctypes.byref(rl)) & 0xffffffff) == 0:
        name = buf.raw[4:rl.value].split(b"\x00\x00")[0].decode("utf-16-le", "replace")
    buf2 = ctypes.create_string_buffer(0x2000)
    rl2 = ctypes.c_ulong()
    if (ntdll.NtQueryObject(dup, 2, buf2, 0x2000, ctypes.byref(rl2)) & 0xffffffff) == 0:
        typ = buf2.raw[4:rl2.value].split(b"\x00")[0].decode("utf-16-le", "replace")
    k32.CloseHandle(dup)
    k32.CloseHandle(hproc)
    return (typ, name)


def mods(pid):
    TH32CS_SNAPMODULE = 0x8
    TH32CS_SNAPMODULE32 = 0x10

    class MODULEENTRY32(ctypes.Structure):
        _fields_ = [("dwSize", w.DWORD), ("th32ModuleID", w.DWORD), ("th32ProcessID", w.DWORD), ("GlblcntUsage", w.DWORD),
                    ("ProccntUsage", w.DWORD), ("modBaseAddr", ctypes.POINTER(ctypes.c_byte)), ("modBaseSize", w.DWORD),
                    ("hModule", w.HMODULE), ("szModule", ctypes.c_char * 256), ("szExePath", ctypes.c_char * 260)]

    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, pid)
    me = MODULEENTRY32()
    me.dwSize = ctypes.sizeof(MODULEENTRY32)
    out = {}
    if k32.Module32First(snap, ctypes.byref(me)):
        while True:
            base = ctypes.cast(me.modBaseAddr, ctypes.c_void_p).value or 0
            out[me.szModule.decode("gb18030", "replace").lower()] = (base, me.modBaseSize)
            if not k32.Module32Next(snap, ctypes.byref(me)):
                break
    k32.CloseHandle(snap)
    return out


def which(ml, addr):
    best = None
    for nm, (b, s) in ml.items():
        if b <= addr < b + s:
            if best is None or b > best[0]:
                best = (b, nm)
    return (best[1], addr - best[0]) if best else (None, 0)


def main():
    our_nt = k32.GetModuleHandleW("ntdll.dll")
    wait_fns = {}
    for fn in ("NtWaitForSingleObject", "NtWaitForMultipleObjects", "NtDelayExecution", "NtWaitForAlertByThreadId", "NtRemoveIoCompletion"):
        a = k32.GetProcAddress(our_nt, fn.encode())
        if a:
            wait_fns[fn] = a - our_nt
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
    ml = {}
    ctx = ctypes.create_string_buffer(1232)
    print("pid=%d" % pid)
    caught = 0
    next_refresh = 0.4
    while time.time() - t0 < 2.5 and caught < 4:
        if k32.WaitForSingleObject(pi.hProcess, 0) == 0:
            print("[%.2f] EXITED" % (time.time() - t0))
            break
        if time.time() - t0 > next_refresh:
            m = mods(pid)
            if m:
                ml = m
            next_refresh += 0.4
        struct.pack_into("<I", ctx, 0x30, CONTEXT_FULL)
        k32.SuspendThread(pi.hThread)
        k32.GetThreadContext(pi.hThread, ctx)
        regs = {n: struct.unpack_from("<Q", ctx, o)[0] for n, o in
                (("Rcx", 0x80), ("Rdx", 0x88), ("R8", 0xB8), ("R9", 0xC0), ("R10", 0xC8), ("Rsp", 0x98), ("Rip", 0xF8))}
        nb, ns = ml.get("ntdll.dll", (0, 0))
        rip_off = regs["Rip"] - nb if nb and nb <= regs["Rip"] < nb + ns else None
        tag = None
        if rip_off is not None:
            for fn, off in wait_fns.items():
                if off <= rip_off < off + 0x60:
                    tag = fn
        if tag == "NtWaitForSingleObject":
            caught += 1
            print("[%.2f] %s R10=0x%X timeout@R8=0x%X" % (time.time() - t0, tag, regs["R10"], regs["R8"]))
            info = obj_info(pid, regs["R10"])
            print("        handle 0x%X -> %s" % (regs["R10"], info if info else "(still failed)"))
            tv = rpm(h, regs["R8"], 8)
            if tv:
                q = struct.unpack("<q", tv)[0]
                print("        timeout = %d (100ns) = %.1f ms" % (q, -q / 10000.0 if q < 0 else 0))
            stack = rpm_tol(h, regs["Rsp"] - 0x1000, 0x100000)
            rets = []
            for i in range(0, len(stack) - 8, 8):
                q2 = struct.unpack_from("<Q", stack, i)[0]
                nm2, off2 = which(ml, q2) if q2 > 0x10000 else (None, 0)
                if nm2:
                    rets.append((q2, nm2, off2))
                if len(rets) >= 20:
                    break
            for q2, nm2, off2 in rets:
                print("        stack: 0x%X -> %s+0x%X" % (q2, nm2, off2))
        k32.ResumeThread(pi.hThread)
        time.sleep(0.01)


if __name__ == "__main__":
    main()


