"""Capture system-wide OutputDebugString output (DBWIN protocol, no injection).

The JX3 client's KGLog / Lua Log() output is emitted via OutputDebugStringA. Only one
listener can exist system-wide (DBWIN_BUFFER). This tool creates the shared buffer and
prints lines, optionally filtered to one pid.

Usage: python watch_debug_out.py [--pid N] [--log FILE] [--secs N]
"""
import argparse
import ctypes
import ctypes.wintypes as w
import sys
import time

k32 = ctypes.windll.kernel32

PAGE_READWRITE = 0x04
FILE_MAP_READ = 0x0004
WAIT_OBJECT_0 = 0

k32.CreateFileMappingA.restype = ctypes.c_void_p
k32.CreateFileMappingA.argtypes = [ctypes.c_void_p, ctypes.c_void_p, w.DWORD,
                                   w.DWORD, w.DWORD, ctypes.c_char_p]
k32.MapViewOfFile.restype = ctypes.c_void_p
k32.MapViewOfFile.argtypes = [ctypes.c_void_p, w.DWORD, w.DWORD, w.DWORD, ctypes.c_size_t]
k32.CreateEventA.restype = ctypes.c_void_p
k32.CreateEventA.argtypes = [ctypes.c_void_p, w.BOOL, w.BOOL, ctypes.c_char_p]
k32.WaitForSingleObject.restype = w.DWORD
k32.WaitForSingleObject.argtypes = [ctypes.c_void_p, w.DWORD]
k32.SetEvent.restype = w.BOOL
k32.SetEvent.argtypes = [ctypes.c_void_p]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pid", type=int, default=0)
    ap.add_argument("--log", default=None)
    ap.add_argument("--secs", type=float, default=1800.0)
    args = ap.parse_args()

    hmap = k32.CreateFileMappingA(ctypes.c_void_p(-1), None, PAGE_READWRITE, 0, 4096 + 8,
                                  b"DBWIN_BUFFER")
    if not hmap:
        print("CreateFileMapping failed err=%d (another listener running?)" % k32.GetLastError())
        return 1
    p = k32.MapViewOfFile(hmap, FILE_MAP_READ, 0, 0, 4096 + 8)
    if not p:
        print("MapViewOfFile failed err=%d" % k32.GetLastError())
        return 1
    ev_ready = k32.CreateEventA(None, False, False, b"DBWIN_BUFFER_READY")
    ev_data = k32.CreateEventA(None, False, False, b"DBWIN_DATA_READY")
    if not ev_ready or not ev_data:
        print("CreateEvent failed err=%d" % k32.GetLastError())
        return 1
    logf = open(args.log, "a", encoding="utf-8") if args.log else None
    print("listening for OutputDebugString pid_filter=%s" % (args.pid or "any"), flush=True)
    k32.SetEvent(ev_ready)
    t0 = time.time()
    while time.time() - t0 < args.secs:
        r = k32.WaitForSingleObject(ev_data, 1000)
        if r != WAIT_OBJECT_0:
            continue
        pid = ctypes.c_uint32.from_address(p).value
        raw = ctypes.string_at(p + 4, 4096)
        s = raw.split(b"\x00", 1)[0].decode("gb18030", "replace")
        if not args.pid or pid == args.pid:
            line = "[%s pid=%d] %s" % (time.strftime("%H:%M:%S"), pid, s.rstrip())
            print(line, flush=True)
            if logf:
                logf.write(line + "\n")
                logf.flush()
        k32.SetEvent(ev_ready)
    print("done", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
