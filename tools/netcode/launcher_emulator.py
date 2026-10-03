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

CLIENT_DIR = os.path.dirname(EXE)
DOWNLOADER = os.path.join(CLIENT_DIR, "KGPK4_StreamDownloaderX64.exe")
CREATE_SUSPENDED = 0x4


def launch(path, workdir, suspend=False):
    si = STARTUPINFO()
    si.cb = ctypes.sizeof(STARTUPINFO)
    pi = PROCESS_INFORMATION()
    flags = CREATE_SUSPENDED if suspend else 0
    ok = k32.CreateProcessW(path, None, None, None, False, flags, None, workdir,
                            ctypes.byref(si), ctypes.byref(pi))
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

    cpi = launch(EXE, workdir, suspend)
    if cpi is None:
        return 1
    pid = cpi.dwProcessId
    print("client pid=%d suspend=%s block=%s workdir=%s" % (pid, suspend, block_kind, workdir), flush=True)
    write_block(pid, block_kind)
    if suspend:
        k32.ResumeThread(cpi.hThread)

    t0 = time.time()
    seen_children = set()
    seen_windows = set()
    exited = None
    while time.time() - t0 < observe:
        el = time.time() - t0
        if k32.WaitForSingleObject(cpi.hProcess, 0) == 0:
            code = ctypes.c_ulong()
            k32.GetExitCodeProcess(cpi.hProcess, ctypes.byref(code))
            exited = code.value
            print("[%.2f] client EXITED code=0x%X" % (el, exited), flush=True)
            break
        for p, par, nm in processes():
            if par == pid and p not in seen_children:
                seen_children.add(p)
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
