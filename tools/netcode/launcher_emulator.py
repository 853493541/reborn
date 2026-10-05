"""V2 P1.3: launcher emulator (production conditions).

Reproduces the real launcher's launch shape with NO diagnostic patches:
  1. launch KGPK4_StreamDownloaderX64.exe (detached, no args) first, like the launcher;
  2. launch JX3ClientX64.exe without suspending it (the launcher uses a plain
     CreateProcessW: no args, inherit=FALSE, env=NULL);
  3. do NOT create the PID-keyed block - the client creates/consumes it itself.
Then monitors windows, children and the client's lifetime for N seconds.

Options: --no-downloader, --block synth|zero, --suspend, --observe N, --workdir path.
"""
import ctypes
import ctypes.wintypes as w
import os
import struct
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, r"C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode")

from probe_state_timeline import (
    EXE, CWD, k32, BLOCK, NAME_FMT, INVALID_HANDLE_VALUE, FILE_MAP_ALL_ACCESS,
    STARTUPINFO, PROCESS_INFORMATION, build_block,
)
from probe_logpatch import scan_root, read_mem, write_mem, ROOT_WIDE, ROOT_STR, NEW_WIDE

CLIENT_DIR = os.path.dirname(EXE)
DOWNLOADER = os.path.join(CLIENT_DIR, "KGPK4_StreamDownloaderX64.exe")
CREATE_SUSPENDED = 0x4


def launch(path, workdir, suspend=False, stdout_path=None):
    si = STARTUPINFO()
    si.cb = ctypes.sizeof(STARTUPINFO)
    pi = PROCESS_INFORMATION()
    flags = CREATE_SUSPENDED if suspend else 0
    inherit = False
    hout = None
    if stdout_path:
        class SECURITY_ATTRIBUTES(ctypes.Structure):
            _fields_ = [("nLength", w.DWORD), ("lpSecurityDescriptor", ctypes.c_void_p),
                        ("bInheritHandle", w.BOOL)]
        sa = SECURITY_ATTRIBUTES()
        sa.nLength = ctypes.sizeof(SECURITY_ATTRIBUTES)
        sa.bInheritHandle = True
        k32.CreateFileW.restype = w.HANDLE
        k32.CreateFileW.argtypes = [w.LPCWSTR, w.DWORD, w.DWORD, ctypes.c_void_p,
                                    w.DWORD, w.DWORD, w.HANDLE]
        hout = k32.CreateFileW(stdout_path, 0x40000000, 0x3, ctypes.byref(sa), 4, 0x80, None)
        if hout and hout != w.HANDLE(-1).value:
            si.dwFlags = 0x100
            si.hStdOutput = hout
            si.hStdError = hout
            si.hStdInput = hout
            inherit = True
    ok = k32.CreateProcessW(path, None, None, None, inherit, flags, None, workdir,
                            ctypes.byref(si), ctypes.byref(pi))
    if hout:
        k32.CloseHandle(hout)
    if not ok:
        print("CreateProcess failed for %s err=%d" % (path, k32.GetLastError()))
        return None
    return pi


def write_block(pid, kind):
    if kind == "none":
        return
    name = (NAME_FMT % pid).encode()
    hmap = k32.CreateFileMappingA(INVALID_HANDLE_VALUE, None, 4, 0, BLOCK, name)
    p = k32.MapViewOfFile(hmap, FILE_MAP_ALL_ACCESS, 0, 0, BLOCK)
    if kind == "synth":
        data = bytes(build_block())
    else:
        data = bytes(BLOCK)
    ctypes.memmove(p, data, BLOCK)
    k32.UnmapViewOfFile(p)
    print("block(%s) written for pid=%d" % (kind, pid))


def write_mem(h, addr, data):
    old = w.DWORD()
    k32.VirtualProtectEx(h, ctypes.c_void_p(addr), len(data), 0x04, ctypes.byref(old))
    n = ctypes.c_size_t()
    ok = (k32.WriteProcessMemory(h, ctypes.c_void_p(addr), data, len(data), ctypes.byref(n))
          and n.value == len(data))
    k32.VirtualProtectEx(h, ctypes.c_void_p(addr), len(data), old.value, ctypes.byref(old))
    return ok


def processes():
    TH32CS_SNAPPROCESS = 0x2

    class PROCESSENTRY32(ctypes.Structure):
        _fields_ = [("dwSize", w.DWORD), ("cntUsage", w.DWORD), ("th32ProcessID", w.DWORD),
                    ("th32DefaultHeapID", ctypes.POINTER(ctypes.c_ulong)), ("th32ModuleID", w.DWORD),
                    ("cntThreads", w.DWORD), ("th32ParentProcessID", w.DWORD),
                    ("pcPriClassBase", ctypes.c_long), ("dwFlags", w.DWORD),
                    ("szExeFile", ctypes.c_char * 260)]
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    pe = PROCESSENTRY32()
    pe.dwSize = ctypes.sizeof(PROCESSENTRY32)
    out = []
    if k32.Process32First(snap, ctypes.byref(pe)):
        while True:
            out.append((pe.th32ProcessID, pe.th32ParentProcessID, pe.szExeFile.decode("gb18030", "replace")))
            if not k32.Process32Next(snap, ctypes.byref(pe)):
                break
    k32.CloseHandle(snap)
    return out


def windows(pid):
    user32 = ctypes.windll.user32
    WNDENUMPROC = ctypes.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
    found = []

    def cb(hwnd, lparam):
        wpid = w.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(wpid))
        if wpid.value == pid:
            buf = ctypes.create_unicode_buffer(256)
            user32.GetWindowTextW(hwnd, buf, 256)
            cls = ctypes.create_unicode_buffer(256)
            user32.GetClassNameW(hwnd, cls, 256)
            found.append((hwnd, bool(user32.IsWindowVisible(hwnd)), cls.value, buf.value))
        return True
    user32.EnumWindows(WNDENUMPROC(cb), 0)
    return found


def main():
    observe = 25.0
    block_kind = "none"
    use_downloader = True
    suspend = False
    workdir = CWD
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--observe" and i + 1 < len(args):
            observe = float(args[i + 1])
        elif a == "--block" and i + 1 < len(args):
            block_kind = args[i + 1]
        elif a == "--no-downloader":
            use_downloader = False
        elif a == "--suspend":
            suspend = True
        elif a == "--workdir" and i + 1 < len(args):
            workdir = args[i + 1]

    if use_downloader and os.path.isfile(DOWNLOADER):
        dpi = launch(DOWNLOADER, CLIENT_DIR)
        if dpi:
            print("downloader pid=%d" % dpi.dwProcessId, flush=True)

    log_capture = "--log-capture" in args
    log_flags = ("--log-flags" in args) or log_capture
    console_path = r"C:\jx3tmp\client_console.txt" if log_flags else None
    cpi = launch(EXE, workdir, suspend, console_path)
    if cpi is None:
        return 1
    pid = cpi.dwProcessId
    print("client pid=%d suspend=%s block=%s workdir=%s log_flags=%s"
          % (pid, suspend, block_kind, workdir, log_flags), flush=True)
    write_block(pid, block_kind)
    if suspend:
        k32.ResumeThread(cpi.hThread)
    if "--patch-upd" in args:
        # DIAGNOSTIC: flip the LaunchUpdater event-3 compare (cmp edx,3 -> cmp edx,4) so the
        # module's event-3 override returns 1 instead of aborting the chain
        k32.OpenProcess.restype = w.HANDLE
        hp = k32.OpenProcess(0x438, False, pid)
        if hp:
            for _ in range(50):
                base = None
                for p, par, nm in processes():
                    pass
                # find the main module base via Toolhelp modules
                TH32CS_SNAPMODULE = 0x8
                class ME32(ctypes.Structure):
                    _fields_ = [("dwSize", w.DWORD), ("th32ModuleID", w.DWORD), ("th32ProcessID", w.DWORD),
                                ("GlblcntUsage", w.DWORD), ("ProccntUsage", w.DWORD),
                                ("modBaseAddr", ctypes.POINTER(ctypes.c_byte)), ("modBaseSize", w.DWORD),
                                ("hModule", w.HMODULE), ("szModule", ctypes.c_char * 256),
                                ("szExePath", ctypes.c_char * 260)]
                snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, pid)
                me = ME32()
                me.dwSize = ctypes.sizeof(ME32)
                if k32.Module32First(snap, ctypes.byref(me)):
                    while True:
                        if me.szModule.decode("gb18030", "replace").lower().startswith("jx3client"):
                            base = ctypes.cast(me.modBaseAddr, ctypes.c_void_p).value
                            break
                        if not k32.Module32Next(snap, ctypes.byref(me)):
                            break
                k32.CloseHandle(snap)
                if base:
                    n = ctypes.c_size_t()
                    ok = k32.WriteProcessMemory(hp, ctypes.c_void_p(base + 0xB1BF6), b"\x04", 1, ctypes.byref(n))
                    print("patch-upd: cmp edx,3 -> 4 %s (base=0x%X)" % ("ok" if ok else "FAIL", base), flush=True)
                    break
                time.sleep(0.05)

    cfg_e10 = None
    for i, a in enumerate(args):
        if a == "--cfg-e10" and i + 1 < len(args):
            cfg_e10 = int(args[i + 1])
    hp = k32.OpenProcess(0x438, False, pid) if (cfg_e10 is not None or log_flags) else None
    exe_base = None
    eng_base = None
    t0 = time.time()
    seen_children = set()
    seen_windows = set()
    exited = None
    root_hits = []
    root_patched = False
    root_restored = False
    viewer_spawned = [False]
    while time.time() - t0 < observe:
        el = time.time() - t0
        if hp and (exe_base is None or eng_base is None):
            TH32CS_SNAPMODULE = 0x8

            class ME32(ctypes.Structure):
                _fields_ = [("dwSize", w.DWORD), ("th32ModuleID", w.DWORD), ("th32ProcessID", w.DWORD),
                            ("GlblcntUsage", w.DWORD), ("ProccntUsage", w.DWORD),
                            ("modBaseAddr", ctypes.POINTER(ctypes.c_byte)), ("modBaseSize", w.DWORD),
                            ("hModule", w.HMODULE), ("szModule", ctypes.c_char * 256),
                            ("szExePath", ctypes.c_char * 260)]
            snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, pid)
            me = ME32()
            me.dwSize = ctypes.sizeof(ME32)
            if k32.Module32First(snap, ctypes.byref(me)):
                while True:
                    nm = me.szModule.decode("gb18030", "replace").lower()
                    if nm.startswith("jx3client") and exe_base is None:
                        exe_base = ctypes.cast(me.modBaseAddr, ctypes.c_void_p).value
                    elif nm.startswith("engine_lua5x64") and eng_base is None:
                        eng_base = ctypes.cast(me.modBaseAddr, ctypes.c_void_p).value
                    if not k32.Module32Next(snap, ctypes.byref(me)):
                        break
            k32.CloseHandle(snap)
        if hp and exe_base and eng_base and log_flags and el < 3.0:
            buf = ctypes.create_string_buffer(8)
            n = ctypes.c_size_t()
            if k32.ReadProcessMemory(hp, ctypes.c_void_p(exe_base + 0xA8C1C8), buf, 8, ctypes.byref(n)):
                cm = struct.unpack("<Q", buf.raw)[0]
                if cm:
                    write_mem(hp, cm + 0x224, struct.pack("<I", 1))
            fl = ctypes.create_string_buffer(4)
            if k32.ReadProcessMemory(hp, ctypes.c_void_p(eng_base + 0x174020), fl, 4, ctypes.byref(n)):
                cur = struct.unpack("<I", fl.raw)[0]
                if (cur & 6) != 6:
                    write_mem(hp, eng_base + 0x174020, struct.pack("<I", cur | 6))
            jb = ctypes.create_string_buffer(6)
            if k32.ReadProcessMemory(hp, ctypes.c_void_p(eng_base + 0xE550F), jb, 6, ctypes.byref(n)):
                if jb.raw != b"\x90" * 6:
                    write_mem(hp, eng_base + 0xE550F, b"\x90" * 6)
                    print("[%.2f] log flags applied (exe=0x%X eng=0x%X)" % (el, exe_base, eng_base), flush=True)
        if hp and exe_base:
            buf = ctypes.create_string_buffer(8)
            n = ctypes.c_size_t()
            if k32.ReadProcessMemory(hp, ctypes.c_void_p(exe_base + 0xA8C1C8), buf, 8, ctypes.byref(n)):
                cm = struct.unpack("<Q", buf.raw)[0]
                if cm:
                    k32.WriteProcessMemory(hp, ctypes.c_void_p(cm + 0xE10),
                                           struct.pack("<I", cfg_e10), 4, ctypes.byref(n))
        if hp and exe_base and log_capture:
            if not root_hits and el > 0.4:
                root_hits, scanned = scan_root(hp)
                print("[%.2f] log-capture: root scan %d hits (%.0f MB)"
                      % (el, len(root_hits), scanned / 1048576.0), flush=True)
            if root_hits and not root_patched and el > 0.5:
                root_patched = True
                for va, ln, s in root_hits:
                    new = NEW_WIDE + b"\x00" * (ln - len(NEW_WIDE)) if ln >= len(NEW_WIDE) else NEW_WIDE
                    write_mem(hp, va, new)
                if eng_base:
                    write_mem(hp, eng_base + 0x170060, b"C:\\jx3t\\\x00")
                lit = read_mem(hp, exe_base + 0x955228, 16)
                if lit and not lit.startswith(b"\\bin64\\lv.exe"):
                    write_mem(hp, exe_base + 0x955228, b"\\bin64\\lv.exe" + b"\x00" * 3)
                print("[%.2f] log-capture: root -> C:\\jx3t\\ + viewer literal" % el, flush=True)
            if root_patched and not root_restored and el > 1.05 and viewer_spawned[0]:
                root_restored = True
                for va, ln, s in root_hits:
                    new = ROOT_WIDE + b"\x00" * (ln - len(ROOT_WIDE)) if ln >= len(ROOT_WIDE) else ROOT_WIDE
                    write_mem(hp, va, new)
                if eng_base:
                    write_mem(hp, eng_base + 0x170060, ROOT_STR.encode() + b"\x00")
                print("[%.2f] log-capture: root RESTORED (viewer running)" % el, flush=True)
        if k32.WaitForSingleObject(cpi.hProcess, 0) == 0:
            code = ctypes.c_ulong()
            k32.GetExitCodeProcess(cpi.hProcess, ctypes.byref(code))
            exited = code.value
            print("[%.2f] client EXITED code=0x%X" % (el, exited), flush=True)
            break
        for p, par, nm in processes():
            if par == pid and p not in seen_children:
                seen_children.add(p)
                if nm.lower() in ("lv.exe", "xlogv.exe"):
                    viewer_spawned[0] = True
                print("[%.2f] child %s pid=%d" % (el, nm, p), flush=True)
        for hwnd, vis, cls, title in windows(pid):
            if hwnd not in seen_windows:
                seen_windows.add(hwnd)
                print("[%.2f] window class=%r visible=%s title=%r" % (el, cls, vis, title), flush=True)
        time.sleep(0.1)
    if exited is None:
        print("[%.2f] still alive at observe limit" % (time.time() - t0), flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
