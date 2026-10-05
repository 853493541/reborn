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
PASSIVE = os.environ.get("PASSIVE", "") != ""
PUMP_DRIVER = os.environ.get("PUMP_DRIVER", "") != ""

TARGETS = {
    0x189BF0: "RG",
    0x189CE9: "RGSTATE",
    0x79F020: "DESTROY",
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
    events_dumped = [False]
    fire_dumped = [False]
    verify_called = [False]
    poll_last = {}
    last_pump = [0.0]
    stepping = [None]
    t0 = time.time()
    de = DEBUG_EVENT()
    ctx = ctypes.create_string_buffer(1232)
    poll_ms = int(os.environ.get("POLL_MS", "100"))
    while True:
        got = k32.WaitForDebugEvent(ctypes.byref(de), poll_ms)
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
                    if rva not in armed and not PASSIVE:
                        ob = read_mem(hproc.value, exe_base + rva, 1)
                        if ob:
                            k32.WriteProcessMemory(hproc, ctypes.c_void_p(exe_base + rva),
                                                   b"\xCC", 1, ctypes.byref(ctypes.c_size_t()))
                            armed[rva] = ob
                            print("[%.2f] armed %s (exe+0x%X)" % (el, TARGETS[rva], rva), flush=True)
                if (gw_client[0] and not verify_called[0]
                        and os.path.exists(r"C:\jx3tmp\call_verify")):
                    verify_called[0] = True
                    try:
                        os.remove(r"C:\jx3tmp\call_verify")
                    except OSError:
                        pass
                    k32.CreateRemoteThread.restype = w.HANDLE
                    k32.CreateRemoteThread.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_size_t,
                                                       ctypes.c_void_p, ctypes.c_void_p, w.DWORD,
                                                       ctypes.POINTER(w.DWORD)]
                    tid = w.DWORD()
                    th = k32.CreateRemoteThread(hproc, None, 0,
                                                ctypes.c_void_p(exe_base + 0x20C930),
                                                ctypes.c_void_p(0), 0,
                                                ctypes.byref(tid))
                    print("[%.2f] CALL Login_AccountVerify (binding 0x20C930) thread=%s"
                          % (el, th), flush=True)
                if (gw_client[0] and not fire_dumped[0]
                        and os.path.exists(r"C:\jx3tmp\dump_fire")):
                    fire_dumped[0] = True
                    try:
                        os.remove(r"C:\jx3tmp\dump_fire")
                    except OSError:
                        pass
                    obj = read_u64(hproc.value, exe_base + 0xA755C0)
                    print("=== dispatcher object=0x%X ===" % (obj or 0), flush=True)
                    if obj:
                        vt = read_u64(hproc.value, obj)
                        print("  vtable=0x%X" % (vt or 0), flush=True)
                        mod = None
                        for b, size, nm, path in module_list(pid):
                            if b <= (vt or 0) < b + size:
                                mod = (nm, b)
                                break
                        print("  vtable module: %s base=0x%X" % (mod if mod else ("?", 0)), flush=True)
                        if vt:
                            for off in (0x660, 0x668, 0x670):
                                fn = read_u64(hproc.value, vt + off)
                                if fn:
                                    fmod = None
                                    for b, size, nm, path in module_list(pid):
                                        if b <= fn < b + size:
                                            fmod = nm
                                            break
                                    print("  vt+0x%X = 0x%X (%s +0x%X)" % (off, fn, fmod or "?", (fn - (mod[1] if mod else 0)) if fmod else 0), flush=True)
                    print("=== end fire dump ===", flush=True)
                if (gw_client[0] and not events_dumped[0]
                        and os.path.exists(r"C:\jx3tmp\dump_events")):
                    events_dumped[0] = True
                    try:
                        os.remove(r"C:\jx3tmp\dump_events")
                    except OSError:
                        pass
                    str_va = exe_base + 0x7DE728
                    base_reg = exe_base + 0x9FB000
                    data = read_mem(hproc.value, base_reg, 0x800)
                    print("=== event registry dump @0x%X (HANDSHAKE_SUCCESS str 0x%X) ==="
                          % (base_reg, str_va), flush=True)
                    if data:
                        for i in range(0, len(data) - 16, 16):
                            name_ptr = struct.unpack_from("<Q", data, i)[0]
                            eid = struct.unpack_from("<Q", data, i + 8)[0]
                            if 0x7FF000000000 < name_ptr < 0x800000000000 and 0 < eid < 0x1000:
                                s = read_mem(hproc.value, name_ptr, 64)
                                if s:
                                    nm = s.split(b"\x00")[0].decode("latin-1", "replace")
                                    if nm and all(32 <= ord(c) < 127 for c in nm):
                                        print("  id=%-4d %s" % (eid, nm), flush=True)
                    print("=== end registry dump ===", flush=True)
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
                if gw_client[0] is None and exe_base and el > 5.0:
                    gw_client[0] = exe_base + 0xA755F0
                    print("[%.2f] gwClient=0x%X (static)" % (el, gw_client[0]), flush=True)
                if PUMP_DRIVER and gw_client[0] and el - last_pump[0] > 0.5:
                    last_pump[0] = el
                    k32.CreateRemoteThread.restype = w.HANDLE
                    k32.CreateRemoteThread.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_size_t,
                                                       ctypes.c_void_p, ctypes.c_void_p, w.DWORD,
                                                       ctypes.POINTER(w.DWORD)]
                    tid = w.DWORD()
                    k32.CreateRemoteThread(hproc, None, 0,
                                           ctypes.c_void_p(exe_base + 0x185900),
                                           ctypes.c_void_p(gw_client[0]), 0,
                                           ctypes.byref(tid))
                if gw_client[0]:
                    st = read_u32(hproc.value, gw_client[0] + 0x1250)
                    f1 = read_u32(hproc.value, gw_client[0] + 0x1268)
                    p28 = read_u64(hproc.value, gw_client[0] + 0x28) or 0
                    p38 = read_u64(hproc.value, gw_client[0] + 0x38) or 0
                    h1 = read_u64(hproc.value, gw_client[0] + 0x1258) or 0
                    h2 = read_u64(hproc.value, gw_client[0] + 0x1260) or 0
                    wgobj = read_u64(hproc.value, exe_base + 0xA8C248) or 0
                    wgflag = read_u32(hproc.value, wgobj + 0x28) if wgobj else None
                    smobj = read_u64(hproc.value, exe_base + 0xA8C268) or 0
                    smflag = read_u32(hproc.value, smobj + 0x20) if smobj else None
                    f30 = read_u32(hproc.value, gw_client[0] + 0x30)
                    inner = read_u64(hproc.value, p28 + 0x10) if p28 else 0
                    ifd = read_u32(hproc.value, inner + 0x10) if inner else None
                    ierr = read_u32(hproc.value, inner + 0x38) if inner else None
                    icb = read_u64(hproc.value, inner + 0x48) if inner else None
                    scst = read_u32(hproc.value, inner + 0x3c) if inner else None
                    rcst = read_u32(hproc.value, inner + 0x40) if inner else None
                    rcb = read_u64(hproc.value, inner + 0x50) if inner else None
                    gm = exe_base + 0xA8C4F0
                    gip = read_mem(hproc.value, gm + 0xe410, 16)
                    gstr = read_mem(hproc.value, gm + 0xe438, 16)
                    gport = read_u32(hproc.value, gm + 0xe430)
                    grole = read_u32(hproc.value, gm + 0xe434)
                    gtr = read_u64(hproc.value, gm + 0xe3f8) or 0
                    key = (st, f1, p28, p38, h1, h2, wgflag, smflag, f30, inner, ifd, ierr, icb,
                           scst, rcst, rcb, gport, grole, gtr, gip, gstr)
                    if poll_last.get("v") != key:
                        poll_last["v"] = key
                        print("[%.2f] POLL state=%s f1268=%s p28=0x%X p38=0x%X h=0x%X/0x%X wg=%s sm=%s"
                              % (el, st, f1, p28, p38, h1, h2, wgflag, smflag), flush=True)
                        print("[%.2f]   f30=%s inner=0x%X fd=%s err=%s scb=0x%X scst=0x%X rcst=0x%X rcb=0x%X"
                              % (el, f30, inner, ifd, ierr, icb or 0, scst or 0, rcst or 0, rcb or 0), flush=True)
                        print("[%.2f]   game: port=%s role=%s transport=0x%X ip=%r str=%r"
                              % (el, gport, grole, gtr, gip, gstr), flush=True)
                if (not triggered[0] and gw_client[0]
                        and os.path.exists(r"C:\jx3tmp\trigger_connect")):
                    triggered[0] = True
                    try:
                        os.remove(r"C:\jx3tmp\trigger_connect")
                    except OSError:
                        pass
                    n = ctypes.c_size_t()
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(gw_client[0] + 0x28),
                                           b"\x00" * 8, 8, ctypes.byref(n))
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(gw_client[0] + 0x38),
                                           b"\x00" * 8, 8, ctypes.byref(n))
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
                    if rva == 0x189CE9:
                        eax = struct.unpack_from("<I", ctx, 0x78)[0]
                        info = " new_state=%d" % eax
                    if rva == 0x79F020:
                        rsp = struct.unpack_from("<Q", ctx, 0x98)[0]
                        ret = read_u64(hproc.value, rsp) if rsp else None
                        info = " ret=exe+0x%X" % (ret - exe_base) if ret and ret > exe_base else " ret=?"
                    if rva == 0x79D03D:
                        rdi = struct.unpack_from("<Q", ctx, 0xB0)[0]
                        r12 = struct.unpack_from("<Q", ctx, 0xD8)[0]
                        r13 = struct.unpack_from("<Q", ctx, 0xE0)[0]
                        info = " rdi(transport)=0x%X r12=0x%X r13=0x%X" % (rdi, r12, r13)
                    elif rva == 0x7A0B64:
                        info = " ebx(vt40res)=0x%X" % struct.unpack_from("<Q", ctx, 0x90)[0]
                    elif rva == 0x7A0B9F:
                        info = " eax(proto)=0x%X" % struct.unpack_from("<Q", ctx, 0x78)[0]
                    elif rva == 0x7A0BCC:
                        rax = struct.unpack_from("<Q", ctx, 0x78)[0]
                        tdata = read_mem(hproc.value, rax, 4) if rax else None
                        info = " template=0x%X bytes=%s" % (rax, tdata.hex() if tdata else "?")
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
                    stepping[0] = rva
                    flags = struct.unpack_from("<I", ctx, 0x44)[0]
                    struct.pack_into("<I", ctx, 0x44, flags | 0x100)
                    k32.SetThreadContext(ht, ctx)
                    k32.CloseHandle(ht)
                    k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
                    continue
            if ec == EXCEPTION_SINGLE_STEP and exe_base:
                rva = stepping[0]
                stepping[0] = None
                if rva is not None and rva in TARGETS and rva not in ONCE:
                    k32.WriteProcessMemory(hproc, ctypes.c_void_p(exe_base + rva),
                                           b"\xCC", 1, ctypes.byref(ctypes.c_size_t()))
                k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
                continue
            k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_EXCEPTION_NOT_HANDLED)
            continue
        k32.ContinueDebugEvent(pid, de.dwThreadId, DBG_CONTINUE)
    print("hit counts: %s" % {k[0]: v for k, v in hits.items()}, flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
