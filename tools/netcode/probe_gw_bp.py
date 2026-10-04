"""V2 P2 step 2: gateway chain breakpoint probe.

Spawns JX3ClientX64.exe under DEBUG_ONLY_THIS_PROCESS, creates the launch block, keeps
config+0xE10=0 (stable-client condition), and arms INT3 breakpoints on the gateway chain:

  PUMP    0x185900  gateway per-frame pump (state machine + ProcessPackage)
  STATE   0x189630  connect state machine (state at [rcx+0x1250])
  CONNECT 0x189440  ProcessConnectState / handshake build
  SEND    0x189E50  KGatewayClient::Send
  HELPER  0x187540  handshake tail filler (SDK client-info fields)

Read-only observation of our own probe child; nothing is written to the install.
Usage: python probe_gw_bp.py   (RUN_S env = seconds, default 1800)
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
    STARTUPINFO, PROCESS_INFORMATION, build_block, module_list,
)

DEBUG_ONLY_THIS_PROCESS = 0x2
DBG_CONTINUE = 0x00010002
DBG_EXCEPTION_NOT_HANDLED = 0x80010001
EXCEPTION_DEBUG_EVENT = 1
CREATE_PROCESS_DEBUG_EVENT = 3
EXIT_PROCESS_DEBUG_EVENT = 5
LOAD_DLL_DEBUG_EVENT = 6
EXCEPTION_BREAKPOINT = 0x80000003
EXCEPTION_SINGLE_STEP = 0x80000004
CTX_FULL = 0x10000B
THREAD_ACCESS = 0x0002 | 0x0004 | 0x0008 | 0x0010
RUN_S = float(os.environ.get("RUN_S", "1800"))

TARGETS = {
    0x185900: "PUMP",
    0x189630: "STATE",
    0x189BF0: "RG",
    0x189440: "CONNECT",
    0x189E50: "SEND",
    0x187540: "HELPER",
}
ONCE = {0x185900}
MAX_HITS = 30


class DEBUG_EVENT(ctypes.Structure):
    _fields_ = [("dwDebugEventCode", w.DWORD), ("dwProcessId", w.DWORD),
                ("dwThreadId", w.DWORD), ("__pad", w.DWORD), ("u", ctypes.c_byte * 160)]


k32.WaitForDebugEvent.restype = w.BOOL
k32.WaitForDebugEvent.argtypes = [ctypes.POINTER(DEBUG_EVENT), w.DWORD]
k32.ContinueDebugEvent.restype = w.BOOL
k32.ContinueDebugEvent.argtypes = [w.DWORD, w.DWORD, w.DWORD]
k32.GetThreadContext.restype = w.BOOL
k32.GetThreadContext.argtypes = [w.HANDLE, ctypes.c_void_p]
k32.SetThreadContext.restype = w.BOOL
k32.SetThreadContext.argtypes = [w.HANDLE, ctypes.c_void_p]
k32.OpenThread.restype = w.HANDLE
k32.OpenThread.argtypes = [w.DWORD, w.BOOL, w.DWORD]


def read_mem(h, addr, size):
    buf = ctypes.create_string_buffer(size)
    n = ctypes.c_size_t()
    if k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, size, ctypes.byref(n)) and n.value == size:
        return buf.raw
    return None


def read_u32(h, addr):
    d = read_mem(h, addr, 4)
    return struct.unpack("<I", d)[0] if d else None


def read_u64(h, addr):
    d = read_mem(h, addr, 8)
    return struct.unpack("<Q", d)[0] if d else None


def main():
    si = STARTUPINFO()
    si.cb = ctypes.sizeof(STARTUPINFO)
    pi = PROCESS_INFORMATION()
    ok = k32.CreateProcessW(EXE, None, None, None, False, DEBUG_ONLY_THIS_PROCESS, None, CWD,
                            ctypes.byref(si), ctypes.byref(pi))
    if not ok:
        print("CreateProcess failed err=%d" % k32.GetLastError())
        return 1
    pid = pi.dwProcessId
    print("pid=%d (debug) targets=%s" % (pid, sorted(TARGETS.values())), flush=True)
    name = (NAME_FMT % pid).encode()
    hmap = k32.CreateFileMappingA(INVALID_HANDLE_VALUE, None, 4, 0, BLOCK, name)
    p = k32.MapViewOfFile(hmap, FILE_MAP_ALL_ACCESS, 0, 0, BLOCK)
    ctypes.memmove(p, bytes(build_block()), BLOCK)
    k32.UnmapViewOfFile(p)

    hproc = None
    exe_base = None
    armed = {}
    hits = {}
    state_last = {}
    gw_client = [None]
    triggered = [False]
    forced = [False]
    table_dumped = [False]
    poll_last = {}
    t0 = time.time()
    de = DEBUG_EVENT()
    ctx = ctypes.create_string_buffer(1232)
    while True:
        got = k32.WaitForDebugEvent(ctypes.byref(de), 100)
        el = time.time() - t0
        if el > RUN_S:
            print("run limit reached", flush=True)
            break
        if not got:
            if hproc and exe_base:
                cm2 = read_u64(hproc.value, exe_base + 0xA8C1C8)
                if cm2:
                    cur = read_mem(hproc.value, cm2 + 0xE10, 4)
                    if cur != b"\x00\x00\x00\x00":
                        k32.WriteProcessMemory(hproc, ctypes.c_void_p(cm2 + 0xE10),
                                               b"\x00\x00\x00\x00", 4,
                                               ctypes.byref(ctypes.c_size_t()))
                for rva in TARGETS:
                    if rva not in armed:
                        ob = read_mem(hproc.value, exe_base + rva, 1)
                        if ob:
                            k32.WriteProcessMemory(hproc, ctypes.c_void_p(exe_base + rva),
                                                   b"\xCC", 1, ctypes.byref(ctypes.c_size_t()))
                            armed[rva] = ob
                            print("[%.2f] armed %s (exe+0x%X)" % (el, TARGETS[rva], rva), flush=True)
                if (gw_client[0] and not table_dumped[0]
                        and os.path.exists(r"C:\jx3tmp\dump_table")):
                    table_dumped[0] = True
                    try:
                        os.remove(r"C:\jx3tmp\dump_table")
                    except OSError:
                        pass
                    print("=== gateway protocol table (gwClient=0x%X) ===" % gw_client[0], flush=True)
                    for i in range(256):
                        h = read_u64(hproc.value, gw_client[0] + 0x250 + i * 8)
                        if h:
                            sz = read_u32(hproc.value, gw_client[0] + 0xa50 + i * 8)
                            print("proto=%-3d handler=0x%X (exe+0x%X) min_size=%s"
                                  % (i, h, (h - exe_base) if h > exe_base else 0, sz), flush=True)
                    print("=== end table ===", flush=True)
                if (gw_client[0] and not forced[0]
                        and os.path.exists(r"C:\jx3tmp\force_handshake")):
                    forced[0] = True
                    try:
                        os.remove(r"C:\jx3tmp\force_handshake")
                    except OSError:
                        pass
                    n = ctypes.c_size_t()
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(gw_client[0] + 0x1258),
                                           b"\x00" * 16, 16, ctypes.byref(n))
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(gw_client[0] + 0x1268),
                                           b"\x00\x00\x00\x00", 4, ctypes.byref(n))
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(gw_client[0] + 0x1250),
                                           struct.pack("<I", 2), 4, ctypes.byref(n))
                    p38 = read_u64(hproc.value, gw_client[0] + 0x38) or 0
                    print("[%.2f] FORCE state=2 handle-cleared p38=0x%X" % (el, p38), flush=True)
                if gw_client[0]:
                    st = read_u32(hproc.value, gw_client[0] + 0x1250)
                    f1 = read_u32(hproc.value, gw_client[0] + 0x1268)
                    p28 = read_u64(hproc.value, gw_client[0] + 0x28) or 0
                    p38 = read_u64(hproc.value, gw_client[0] + 0x38) or 0
                    h1 = read_u64(hproc.value, gw_client[0] + 0x1258) or 0
                    h2 = read_u64(hproc.value, gw_client[0] + 0x1260) or 0
                    key = (st, f1, p28, p38, h1, h2)
                    if poll_last.get("v") != key:
                        poll_last["v"] = key
                        print("[%.2f] POLL state=%s f1268=%s p28=0x%X p38=0x%X h=0x%X/0x%X"
                              % (el, st, f1, p28, p38, h1, h2), flush=True)
                if (not triggered[0] and gw_client[0]
                        and os.path.exists(r"C:\jx3tmp\trigger_connect")):
                    triggered[0] = True
                    try:
                        os.remove(r"C:\jx3tmp\trigger_connect")
                    except OSError:
                        pass
                    n = ctypes.c_size_t()
                    host = b"127.0.0.1\x00"
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(gw_client[0]),
                                           host + b"\x00" * (32 - len(host)), 32, ctypes.byref(n))
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(gw_client[0] + 0x20),
                                           struct.pack("<H", 3724), 2, ctypes.byref(n))
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(gw_client[0] + 0x1250),
                                           struct.pack("<I", 0), 4, ctypes.byref(n))
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(gw_client[0] + 0x1268),
                                           struct.pack("<I", 0), 4, ctypes.byref(n))
                    k32.CreateRemoteThread.restype = w.HANDLE
                    k32.CreateRemoteThread.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_size_t,
                                                       ctypes.c_void_p, ctypes.c_void_p, w.DWORD,
                                                       ctypes.POINTER(w.DWORD)]
                    tid = w.DWORD()
                    th = k32.CreateRemoteThread(hproc, None, 0,
                                                ctypes.c_void_p(exe_base + 0x185ED0),
                                                ctypes.c_void_p(gw_client[0]), 0,
                                                ctypes.byref(tid))
                    print("[%.2f] TRIGGER Connect(this=0x%X) host=127.0.0.1:3724 thread=%s"
                          % (el, gw_client[0], th), flush=True)
                    time.sleep(0.3)
                    n2 = ctypes.c_size_t()
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(gw_client[0] + 0x1258),
                                           b"\x00" * 16, 16, ctypes.byref(n2))
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(gw_client[0] + 0x1268),
                                           b"\x00\x00\x00\x00", 4, ctypes.byref(n2))
                    st = read_u32(hproc.value, gw_client[0] + 0x1250)
                    p38 = read_u64(hproc.value, gw_client[0] + 0x38) or 0
                    print("[%.2f] handle cleared after connect (state=%s p38=0x%X)"
                          % (el, st, p38), flush=True)
            continue
        code = de.dwDebugEventCode
        if code == CREATE_PROCESS_DEBUG_EVENT:
            hproc = ctypes.c_void_p(struct.unpack_from("<Q", de.u, 8)[0])
            print("[%.2f] CREATE_PROCESS" % el, flush=True)
            k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
            continue
        if code == LOAD_DLL_DEBUG_EVENT:
            hf = struct.unpack_from("<Q", de.u, 0)[0]
            if hf:
                k32.CloseHandle(ctypes.c_void_p(hf))
            if exe_base is None and hproc:
                for base, size, nm, path in module_list(pid):
                    if nm.lower().startswith("jx3client"):
                        exe_base = base
                        break
            k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
            continue
        if code == EXIT_PROCESS_DEBUG_EVENT:
            print("[%.2f] EXIT_PROCESS" % el, flush=True)
            break
        if code == EXCEPTION_DEBUG_EVENT:
            ec = struct.unpack_from("<I", de.u, 0)[0]
            addr = struct.unpack_from("<Q", de.u, 16)[0]
            if ec == EXCEPTION_BREAKPOINT and exe_base:
                rva = addr - exe_base
                if rva in TARGETS:
                    ht = k32.OpenThread(THREAD_ACCESS, False, de.dwThreadId)
                    struct.pack_into("<I", ctx, 0x30, CTX_FULL)
                    k32.GetThreadContext(ht, ctx)
                    rcx = struct.unpack_from("<Q", ctx, 0x80)[0]
                    rdx = struct.unpack_from("<Q", ctx, 0x88)[0]
                    rbx = struct.unpack_from("<Q", ctx, 0x90)[0]
                    if rva in (0x185900, 0x189630) and rcx:
                        gw_client[0] = rcx
                    info = ""
                    log_it = True
                    if rva == 0x189630 and rcx:
                        st = read_u32(hproc.value, rcx + 0x1250)
                        f1 = read_u32(hproc.value, rcx + 0x1268)
                        p28 = read_u64(hproc.value, rcx + 0x28) or 0
                        p38 = read_u64(hproc.value, rcx + 0x38) or 0
                        h1 = read_u64(hproc.value, rcx + 0x1258) or 0
                        h2 = read_u64(hproc.value, rcx + 0x1260) or 0
                        info = (" state=%s f1268=%s p28=0x%X p38=0x%X h1258=0x%X h1260=0x%X"
                                % (st, f1, p28, p38, h1, h2))
                    nm = TARGETS[rva]
                    key = (nm,)
                    hits[key] = hits.get(key, 0) + 1
                    if log_it and hits[key] <= MAX_HITS:
                        print("[%.2f] %s rcx=0x%X rdx=0x%X rbx=0x%X%s"
                              % (el, nm, rcx, rdx, rbx, info), flush=True)
                    elif log_it and hits[key] == MAX_HITS + 1:
                        print("[%.2f] %s (further hits suppressed)" % (el, nm), flush=True)
                    # restore + single-step + re-arm
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(exe_base + rva),
                                           armed[rva], 1, ctypes.byref(ctypes.c_size_t()))
                    flags = struct.unpack_from("<I", ctx, 0x44)[0]
                    struct.pack_into("<I", ctx, 0x44, flags | 0x100)
                    k32.SetThreadContext(ht, ctx)
                    k32.CloseHandle(ht)
                    k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
                    continue
            if ec == EXCEPTION_SINGLE_STEP and exe_base:
                ht = k32.OpenThread(THREAD_ACCESS, False, de.dwThreadId)
                struct.pack_into("<I", ctx, 0x30, CTX_FULL)
                k32.GetThreadContext(ht, ctx)
                rip = struct.unpack_from("<Q", ctx, 0xF8)[0]
                rva = rip - exe_base - 1
                if rva in TARGETS and rva not in ONCE:
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(exe_base + rva),
                                           b"\xCC", 1, ctypes.byref(ctypes.c_size_t()))
                k32.CloseHandle(ht)
                k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
                continue
            k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_EXCEPTION_NOT_HANDLED)
            continue
        k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
    print("hit counts: %s" % {k[0]: v for k, v in hits.items()}, flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
