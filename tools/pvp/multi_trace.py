"""Attach to a live process, set up to 4 hardware EXECUTION breakpoints (DR0..DR3) by
module+offset, and report per-breakpoint hit counts + first-hit register context.

Read-only w.r.t. process memory (hardware breakpoints live in DR registers). NOTE (2026-10-08):
contrary to the earlier note in the docs, `DebugActiveProcess` does NOT kill the JX3 test client
(measured: a kernel32!GetTickCount hw BP fired 44k times and the client survived). Reuse this to
trace the client when a read-only probe cannot answer "is this function ever called?".

Usage:
  python multi_trace.py <pid> ModA+0xRVA [ModB+0xRVA ...] [--seconds N] [--inject-w]

  ModA is a loaded module name (JX3ClientX64.exe, JX3RepresentX64.dll, ...). --inject-w spawns a
  helper thread that focuses the KGWin32App window and SendInput's W repeatedly during the trace.

  --filter <hex>: only LOG a hit whose rax/rcx/rdx/rsi/rdi/r8/r9 equals that value (hit counting is
  unchanged). Use it to catch one call out of many.

  WARNING: do NOT set an exec BP on a HOT function (e.g. jemalloc/malloc/memcpy/GetTickCount). Every
  hit single-steps the target thread, so a hot BP stalls the target (visible UI flashing / near-hang).
  Prefer: (a) an AV/fault catch (cheap, no BP), or (b) a COLD call site, or (c) --filter with a cold-ish
  site. If it flashes, you hit a hot function - detach (Ctrl-C) and re-arm elsewhere.
"""
from __future__ import annotations

import ctypes
import ctypes.wintypes as w
import struct
import sys
import time

sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
import mini_debugger as M  # noqa: E402

k32 = ctypes.windll.kernel32
TH32CS_SNAPMODULE = 0x8


class ME32(ctypes.Structure):
    _fields_ = [("dwSize", w.DWORD), ("th32ModuleID", w.DWORD), ("th32ProcessID", w.DWORD),
                ("GlblcntUsage", w.DWORD), ("ProccntUsage", w.DWORD),
                ("modBaseAddr", ctypes.c_void_p), ("modBaseSize", w.DWORD),
                ("hModule", w.HMODULE), ("szModule", ctypes.c_char * 256),
                ("szExePath", ctypes.c_char * 260)]


def mod_base(pid, name):
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, pid)
    me = ME32()
    me.dwSize = ctypes.sizeof(ME32)
    ok = k32.Module32First(snap, ctypes.byref(me))
    base = 0
    while ok:
        if me.szModule.decode("latin1").lower() == name.lower():
            base = me.modBaseAddr or 0
            break
        ok = k32.Module32Next(snap, ctypes.byref(me))
    k32.CloseHandle(snap)
    return base


def list_modules(pid):
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPMODULE, pid)
    me = ME32()
    me.dwSize = ctypes.sizeof(ME32)
    mods = []
    ok = k32.Module32First(snap, ctypes.byref(me))
    while ok:
        mods.append((me.modBaseAddr or 0, me.modBaseSize or 0, me.szModule.decode("latin1")))
        ok = k32.Module32Next(snap, ctypes.byref(me))
    k32.CloseHandle(snap)
    return mods


def make_resolver(pid):
    state = {"mods": list_modules(pid)}

    def res(addr):
        for base, size, name in state["mods"]:
            if base and base <= addr < base + size:
                return "%s+0x%X" % (name, addr - base)
        state["mods"] = list_modules(pid)   # engines load after attach; refresh on miss
        for base, size, name in state["mods"]:
            if base and base <= addr < base + size:
                return "%s+0x%X" % (name, addr - base)
        return hex(addr)
    return res


class ADDR64(ctypes.Structure):
    _fields_ = [("Offset", ctypes.c_uint64), ("Segment", ctypes.c_uint16),
                ("Mode", ctypes.c_uint32), ("_pad", ctypes.c_uint16)]


class STACKFRAME64(ctypes.Structure):
    _fields_ = [("AddrPC", ADDR64), ("AddrReturn", ADDR64), ("AddrFrame", ADDR64),
                ("AddrStack", ADDR64), ("AddrBStore", ADDR64), ("FuncTableEntry", ctypes.c_uint64),
                ("Params", ctypes.c_uint64 * 4), ("Far", w.BOOL), ("Virtual", w.BOOL),
                ("Reserved", ctypes.c_uint64 * 3)]


def stackwalk(pid, tid, ctx, res):
    """Walk the faulting thread's stack via dbghelp StackWalk64 (uses each module's .pdata)."""
    try:
        dbghelp = ctypes.windll.dbghelp
        k32.OpenProcess.restype = w.HANDLE
        k32.OpenThread.restype = w.HANDLE
        h = k32.OpenProcess(0x1F0FFF, False, pid)
        ht = k32.OpenThread(0x1F0FFF, False, tid)
        if not h or not ht:
            print("stackwalk: open failed", flush=True)
            return
        dbghelp.SymInitialize.restype = w.BOOL
        dbghelp.SymInitialize(h, None, True)   # TRUE: enumerate the target's modules (loads .pdata)
        dbghelp.SymFunctionTableAccess64.restype = ctypes.c_void_p
        dbghelp.SymFunctionTableAccess64.argtypes = [w.HANDLE, ctypes.c_uint64]
        dbghelp.SymGetModuleBase64.restype = ctypes.c_uint64
        dbghelp.SymGetModuleBase64.argtypes = [w.HANDLE, ctypes.c_uint64]
        dbghelp.StackWalk64.restype = w.BOOL
        sf = STACKFRAME64()
        ctx.ContextFlags = M.CONTEXT_FULL
        for a, v in (("AddrPC", ctx.Rip), ("AddrFrame", ctx.Rbp), ("AddrStack", ctx.Rsp)):
            f = getattr(sf, a)
            f.Offset = v
            f.Mode = 3  # AddrModeFlat
        print("STACKWALK:", flush=True)
        for i in range(48):
            ok = dbghelp.StackWalk64(0x8664, h, ht, ctypes.byref(sf), ctypes.byref(ctx), None,
                                     dbghelp.SymFunctionTableAccess64, dbghelp.SymGetModuleBase64, None)
            if not ok:
                break
            print("  #%02d pc=%s ret=%s" % (i, res(sf.AddrPC.Offset), res(sf.AddrReturn.Offset)),
                  flush=True)
            if sf.AddrPC.Offset == 0:
                break
        dbghelp.SymCleanup(h)
        k32.CloseHandle(ht)
        k32.CloseHandle(h)
    except Exception as e:
        print("stackwalk ex: %s" % e, flush=True)


def write_dump(pid, path):
    """Full-memory minidump of the (paused) target via dbghelp MiniDumpWriteDump."""
    try:
        k32.OpenProcess.restype = w.HANDLE
        k32.CreateFileW.restype = w.HANDLE
        h = k32.OpenProcess(0x1F0FFF, False, pid)
        fh = k32.CreateFileW(path, 0x40000000, 0, None, 2, 0x80, None)  # GENERIC_WRITE, CREATE_ALWAYS
        if not h or not fh:
            print("dump: open failed h=%s fh=%s" % (h, fh), flush=True)
            return False
        dbghelp = ctypes.windll.dbghelp
        # MiniDumpWithFullMemory(2) | WithFullMemoryInfo(0x800) | WithThreadInfo(0x1000)
        ok = dbghelp.MiniDumpWriteDump(h, pid, fh, 0x2802, None, None, None)
        k32.CloseHandle(fh)
        k32.CloseHandle(h)
        print("dump: MiniDumpWriteDump ok=%d -> %s" % (ok, path), flush=True)
        return bool(ok)
    except Exception as e:
        print("dump ex: %s" % e, flush=True)
        return False


def exec_ranges(pid, mods):
    """[(lo,hi,name)] of each module's executable sections (read PE headers from target)."""
    hp = k32.OpenProcess(0x410, False, pid)
    out = []
    if not hp:
        return out
    for base, size, name in mods:
        try:
            buf = ctypes.create_string_buffer(0x1000)
            got = ctypes.c_size_t(0)
            if not k32.ReadProcessMemory(hp, ctypes.c_void_p(base), buf, 0x1000, ctypes.byref(got)):
                continue
            raw = buf.raw
            if raw[0:2] != b"MZ":
                continue
            e = struct.unpack_from("<I", raw, 0x3C)[0]
            nsec = struct.unpack_from("<H", raw, e + 6)[0]
            optsz = struct.unpack_from("<H", raw, e + 0x14)[0]
            so = e + 0x18 + optsz
            for i in range(nsec):
                off = so + i * 40
                if off + 40 > got.value:
                    break
                vsz = struct.unpack_from("<I", raw, off + 8)[0]
                va = struct.unpack_from("<I", raw, off + 0x0C)[0]
                ch = struct.unpack_from("<I", raw, off + 0x24)[0]
                if (ch & 0x20000000) and vsz:
                    out.append((base + va, base + va + max(vsz, 1), base, name))
        except Exception:
            pass
    k32.CloseHandle(hp)
    return out


TH32CS_SNAPTHREAD = 0x4

# Set by --data-write: arm the Dr slots as data WRITE breakpoints (len=4) instead of exec.
BP_WRITE = False
# Set in main(); used by arm() to resolve 'tls:<index>:<off>' targets per-thread.
PID = 0
FIRST_ARM = {}   # tid -> already printed the resolved TLS arm address

_ntdll = ctypes.WinDLL("ntdll")


class _TBI(ctypes.Structure):
    _fields_ = [("ExitStatus", ctypes.c_long), ("TebBaseAddress", ctypes.c_void_p),
                ("UniqueProcess", ctypes.c_void_p), ("UniqueThread", ctypes.c_void_p),
                ("AffinityMask", ctypes.c_void_p), ("Priority", ctypes.c_long),
                ("BasePriority", ctypes.c_long)]


def resolve_tls_field(pid, tid, index, off):
    """Absolute VA of [ TLS block[index] + off ] for a thread (0 if unresolved).

    For the engine DLL's `__declspec(thread)` field: TEB+0x58 = TLS array -> [+index*8] = the
    module's TLS block -> [+off]. Used to arm a HW data BP on a per-thread field.
    """
    try:
        ht = k32.OpenThread(0x0040, False, tid)
        if not ht:
            return 0
        _ntdll.NtQueryInformationThread.argtypes = [ctypes.c_void_p, ctypes.c_int,
                                                    ctypes.c_void_p, ctypes.c_ulong, ctypes.c_void_p]
        _ntdll.NtQueryInformationThread.restype = ctypes.c_long
        tbi = _TBI()
        ret = _ntdll.NtQueryInformationThread(ht, 0, ctypes.byref(tbi), ctypes.sizeof(tbi), None)
        k32.CloseHandle(ht)
        if ret != 0 or not tbi.TebBaseAddress:
            return 0
        hp = k32.OpenProcess(0x410, False, pid)
        if not hp:
            return 0

        def rd(a, n):
            b = ctypes.create_string_buffer(n)
            got = ctypes.c_size_t(0)
            k32.ReadProcessMemory(hp, ctypes.c_void_p(a), b, n, ctypes.byref(got))
            return b.raw[:got.value]

        try:
            b1 = rd(int(tbi.TebBaseAddress) + 0x58, 8)
            if len(b1) < 8:
                return 0
            tlsarr = struct.unpack("<Q", b1)[0]
            b2 = rd(tlsarr + index * 8, 8)
            if len(b2) < 8:
                return 0
            block = struct.unpack("<Q", b2)[0]
        finally:
            k32.CloseHandle(hp)
        return (block + off) if block else 0
    except Exception as e:
        print("  resolve_tls_field tid=%d err=%r" % (tid, e), flush=True)
        return 0


class TE32(ctypes.Structure):
    _fields_ = [("dwSize", w.DWORD), ("cntUsage", w.DWORD), ("th32ThreadID", w.DWORD),
                ("th32OwnerProcessID", w.DWORD), ("tpBasePri", w.LONG), ("tpDeltaPri", w.LONG),
                ("dwFlags", w.DWORD)]


def arm_all(pid, vas):
    """Arm the HW BP on every existing thread of the process (used to arm late)."""
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0)
    te = TE32()
    te.dwSize = ctypes.sizeof(TE32)
    n = 0
    ok = k32.Thread32First(snap, ctypes.byref(te))
    while ok:
        if te.th32OwnerProcessID == pid:
            arm(te.th32ThreadID, vas)
            n += 1
        ok = k32.Thread32Next(snap, ctypes.byref(te))
    k32.CloseHandle(snap)
    return n


def arm(tid, vas):
    ht = k32.OpenThread(0x0008 | 0x0010 | 0x0002 | 0x0040, False, tid)
    if not ht:
        return
    k32.SuspendThread(ht)
    ctx = M.CONTEXT()
    ctx.ContextFlags = M.CONTEXT_DEBUG_REGISTERS
    if k32.GetThreadContext(ht, ctypes.byref(ctx)):
        regs = [0, 0, 0, 0]
        dr7 = 0
        rw = 0x1 if BP_WRITE else 0x0   # 01 = data write, 00 = exec
        ln = 0x3 if BP_WRITE else 0x0   # 11 = 4 bytes,   00 = 1 byte
        for idx, va in enumerate(vas[:4]):
            if isinstance(va, tuple) and va and va[0] == "tls":
                tidx, toff = va[1], va[2]
                va = resolve_tls_field(PID, tid, tidx, toff)
                if tid not in FIRST_ARM:
                    FIRST_ARM[tid] = True
                    print("  ARM tid=%d tls[%d]+0x%X -> 0x%X" % (tid, tidx, toff, va), flush=True)
            regs[idx] = va
            dr7 |= (0x1 << (idx * 2))        # Ln local-enable
            dr7 |= (rw << (16 + idx * 4))    # RWn
            dr7 |= (ln << (18 + idx * 4))    # LENn
        if BP_WRITE:
            dr7 |= 0x400                     # reserved bit 10 (often must be 1 for data BPs)
        ctx.Dr0, ctx.Dr1, ctx.Dr2, ctx.Dr3 = regs
        ctx.Dr7 = dr7
        ctx.ContextFlags = M.CONTEXT_DEBUG_REGISTERS
        k32.SetThreadContext(ht, ctypes.byref(ctx))
    k32.ResumeThread(ht)
    k32.CloseHandle(ht)


def start_injector():
    import threading
    VK_MENU = 0x12
    KEYEVENTF_KEYUP = 0x0002
    KEYEVENTF_SCANCODE = 0x0008
    INPUT_KEYBOARD = 1

    class KI(ctypes.Structure):
        _fields_ = [("wVk", w.WORD), ("wScan", w.WORD), ("dwFlags", w.DWORD),
                    ("time", w.DWORD), ("dwExtraInfo", ctypes.POINTER(ctypes.c_ulong))]

    class INP(ctypes.Structure):
        class _U(ctypes.Union):
            _fields_ = [("ki", KI), ("pad", ctypes.c_byte * 32)]
        _anonymous_ = ("u",)
        _fields_ = [("type", w.DWORD), ("u", _U)]

    def run():
        u32 = ctypes.windll.user32
        found = []

        @ctypes.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
        def cb(h, l):
            b = ctypes.create_unicode_buffer(64)
            u32.GetClassNameW(h, b, 64)
            if b.value == "KGWin32App" and u32.IsWindowVisible(h):
                found.append(h)
            return True
        u32.EnumWindows(cb, 0)
        if found:
            u32.AllowSetForegroundWindow(0xFFFFFFFF)
        fg_ok = 0
        sends = 0
        for _ in range(60):
            # re-assert focus every iteration (the shell/other windows steal it)
            if found:
                for _ in range(4):
                    if u32.GetForegroundWindow() == found[0]:
                        break
                    u32.keybd_event(VK_MENU, 0, 0, 0)
                    u32.keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0)
                    u32.SetForegroundWindow(found[0])
                    time.sleep(0.05)
            fg = (not found) or (u32.GetForegroundWindow() == found[0])
            if fg:
                fg_ok += 1
            inp = INP()
            inp.type = INPUT_KEYBOARD
            inp.ki = KI(0, 0x11, KEYEVENTF_SCANCODE, 0, None)
            u32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INP))
            sends += 1
            time.sleep(0.4)
            inp.ki = KI(0, 0x11, KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP, 0, None)
            u32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INP))
            sends += 1
            time.sleep(0.4)
        print("injector: sends=%d foreground_ok=%d interval" % (sends, fg_ok), flush=True)
    threading.Thread(target=run, daemon=True).start()


def main():
    pid = int(sys.argv[1])
    global PID
    PID = pid
    specs = []
    i = 2
    while i < len(sys.argv) and not sys.argv[i].startswith("--"):
        specs.append(sys.argv[i])
        i += 1
    seconds = 15
    if "--seconds" in sys.argv:
        seconds = int(sys.argv[sys.argv.index("--seconds") + 1])
    filt = None
    if "--filter" in sys.argv:
        filt = int(sys.argv[sys.argv.index("--filter") + 1], 0)
    dump = ""
    if "--dump" in sys.argv:
        dump = sys.argv[sys.argv.index("--dump") + 1]
    do_walk = "--stackwalk" in sys.argv
    arm_after = 0.0
    if "--arm-after" in sys.argv:
        arm_after = float(sys.argv[sys.argv.index("--arm-after") + 1])
    bp_log = "--bp-log" in sys.argv
    global BP_WRITE
    BP_WRITE = "--data-write" in sys.argv

    targets = []
    for s in specs:
        if s.startswith("tls:"):
            _, idx, off = s.split(":")
            print("target %s = per-thread [TLSblock(%d)+0x%X]" % (s, int(idx), int(off, 0)))
            targets.append((s, None, ("tls", int(idx), int(off, 0))))
        elif "+" in s:
            mod, off = s.split("+")
            va = mod_base(pid, mod) + int(off, 0)
            print("target %s+0x%X = 0x%X" % (mod, int(off, 0), va))
            targets.append((mod, int(off, 0), va))
        else:
            va = int(s, 0)
            print("target %s = 0x%X" % (s, va))
            targets.append((s, 0, va))
    if len(targets) > 4:
        print("need 0..4 targets (0 = AV-catch only)")
        return 1

    if "--inject-w" in sys.argv:
        start_injector()

    if not k32.DebugActiveProcess(pid):
        print("attach failed err=%d" % ctypes.get_last_error())
        return 1
    print("attached pid=%d (watching for AV / BPs)" % pid, flush=True)
    res = make_resolver(pid)
    ev = M.DEBUG_EVENT()
    counts = {va: 0 for _, _, va in targets}
    seen_writers = set()   # (tid, rip) already reported for data-write BPs
    first = {}
    dumped = {"done": False}
    vas = [va for _, _, va in targets]
    end = time.time() + seconds
    t0 = time.time()
    armed_once = False
    while time.time() < end:
        if not armed_once and arm_after > 0 and (time.time() - t0) >= arm_after:
            print("arming after %.0fs: %d threads" % (arm_after, arm_all(pid, vas)), flush=True)
            armed_once = True
        if not k32.WaitForDebugEvent(ctypes.byref(ev), 300):
            continue
        code = ev.dwDebugEventCode
        cont = M.DBG_CONTINUE
        if code in (M.CREATE_THREAD_DEBUG_EVENT, M.CREATE_PROCESS_DEBUG_EVENT):
            if arm_after <= 0 or (time.time() - t0) >= arm_after:
                arm(ev.dwThreadId, vas)
        elif code == M.EXCEPTION_DEBUG_EVENT:
            ex_code = struct.unpack_from("<I", ev.u.pad, 0)[0]
            f = struct.unpack_from("<I", ev.u.pad, 4)[0]
            if ex_code == M.EXCEPTION_SINGLE_STEP and f == 0:
                ht = k32.OpenThread(0x0008 | 0x0010, False, ev.dwThreadId)
                ctx = M.CONTEXT()
                ctx.ContextFlags = M.CONTEXT_FULL
                if ht and k32.GetThreadContext(ht, ctypes.byref(ctx)):
                    if BP_WRITE:
                        # data write BP: RIP is the instruction AFTER the writer; which slot fired is in Dr6/Dr7
                        dr6 = ctx.Dr6 & 0xF
                        dr7 = ctx.Dr7
                        for bidx, (m, o, va) in enumerate(targets):
                            if dr6 & (1 << bidx):
                                counts[va] += 1
                                keyv = (ev.dwThreadId, ctx.Rip)
                                if keyv not in seen_writers:
                                    seen_writers.add(keyv)
                                    print("DATAWRITE tid=%d addr=0x%X writer_rip=%s dr6=%s dr7=%s "
                                          "rcx=%s rdx=%s rsi=%s rax=%s" % (
                                              ev.dwThreadId, va, res(ctx.Rip), hex(ctx.Dr6), hex(dr7),
                                              hex(ctx.Rcx), hex(ctx.Rdx), hex(ctx.Rsi), hex(ctx.Rax)),
                                          flush=True)
                        ctx.Dr6 = 0
                    for m, o, va in targets:
                        if ctx.Rip == va:
                            counts[va] += 1
                            if bp_log:
                                print("BPHIT tid=%d %s rcx=%s rdx=%s r8=%s r9=%s rbx=%s r12=%s r15=%s rsi=%s" %
                                      (ev.dwThreadId, "%s+0x%X" % (m, o), hex(ctx.Rcx),
                                       hex(ctx.Rdx), hex(ctx.R8), hex(ctx.R9), hex(ctx.Rbx),
                                       hex(ctx.R12), hex(ctx.R15), hex(ctx.Rsi)), flush=True)
                            if filt is not None and filt not in (ctx.Rax, ctx.Rcx, ctx.Rdx,
                                                                 ctx.Rsi, ctx.Rdi, ctx.R8, ctx.R9):
                                continue   # only log hits carrying the bad size
                            if va not in first:
                                stack = ""
                                hp = k32.OpenProcess(0x410, False, pid)
                                if hp:
                                    sb = ctypes.create_string_buffer(8 * 16)
                                    got = ctypes.c_size_t(0)
                                    if k32.ReadProcessMemory(hp, ctypes.c_void_p(ctx.Rsp), sb,
                                                             8 * 16, ctypes.byref(got)):
                                        fr = [res(struct.unpack_from("<Q", sb.raw, k)[0])
                                              for k in range(0, min(got.value, 8 * 16), 8)]
                                        stack = " stack=[" + " ".join(fr) + "]"
                                    k32.CloseHandle(hp)
                                first[va] = ("tid=%d rax=%s rcx=%s rdx=%s rsi=%s rdi=%s rbp=%s "
                                             "rsp=%s r8=%s r9=%s%s" % (
                                                 ev.dwThreadId, res(ctx.Rax), res(ctx.Rcx),
                                                 res(ctx.Rdx), res(ctx.Rsi), res(ctx.Rdi),
                                                 res(ctx.Rbp), res(ctx.Rsp), res(ctx.R8),
                                                 res(ctx.R9), stack))
                    ctx.EFlags |= 0x10000
                    k32.SetThreadContext(ht, ctypes.byref(ctx))
                if ht:
                    k32.CloseHandle(ht)
            else:
                ex_addr = struct.unpack_from("<Q", ev.u.pad, 16)[0]
                if ex_code == 0xC0000005 and f == 0:   # first-chance AV: dump the fault context
                    ht = k32.OpenThread(0x0008 | 0x0010, False, ev.dwThreadId)
                    ctx = M.CONTEXT()
                    ctx.ContextFlags = M.CONTEXT_FULL
                    if ht and k32.GetThreadContext(ht, ctypes.byref(ctx)):
                        stack = ""
                        chain = ""
                        hp = k32.OpenProcess(0x410, False, pid)
                        if hp:
                            sz = 8 * 128
                            sb = ctypes.create_string_buffer(sz)
                            got = ctypes.c_size_t(0)
                            if k32.ReadProcessMemory(hp, ctypes.c_void_p(ctx.Rsp), sb, sz,
                                                     ctypes.byref(got)):
                                er = exec_ranges(pid, list_modules(pid))
                                resolved = []
                                code = []
                                for k in range(0, min(got.value, sz), 8):
                                    v = struct.unpack_from("<Q", sb.raw, k)[0]
                                    r = res(v)
                                    if r != hex(v):   # points into a loaded module -> a code/data ref
                                        resolved.append("[rsp+0x%X]=%s" % (k, r))
                                    for lo, hi, mb, nm in er:
                                        if lo <= v < hi:
                                            code.append("[rsp+0x%X]=%s+0x%X" % (k, nm, v - mb))
                                            break
                                chain = " chain=[" + " ".join(resolved) + "]"
                                chain += "\nCODECHAIN codechain=[" + " ".join(code) + "]"
                            k32.CloseHandle(hp)
                        print("AV tid=%d av=%s rip=%s rax=%s rcx=%s rdx=%s rsi=%s rdi=%s rbp=%s "
                              "rsp=%s r8=%s r9=%s%s" % (
                                  ev.dwThreadId, res(ex_addr), res(ctx.Rip), res(ctx.Rax),
                                  res(ctx.Rcx), res(ctx.Rdx), res(ctx.Rsi), res(ctx.Rdi),
                                  res(ctx.Rbp), res(ctx.Rsp), res(ctx.R8), res(ctx.R9), stack),
                              flush=True)
                        print("AVCHAIN %s" % chain, flush=True)
                    if ht:
                        k32.CloseHandle(ht)
                    if dump and not dumped["done"]:
                        dumped["done"] = True
                        write_dump(pid, dump)
                    if do_walk and not dumped["done"]:
                        dumped["done"] = True
                        stackwalk(pid, ev.dwThreadId, ctx, res)
                cont = M.DBG_EXCEPTION_NOT_HANDLED
        else:
            if arm_after <= 0 or (time.time() - t0) >= arm_after:
                arm(ev.dwThreadId, vas)
        k32.ContinueDebugEvent(ev.dwProcessId, ev.dwThreadId, cont)
    k32.DebugActiveProcessStop(pid)
    for m, o, va in targets:
        label = ("%s+0x%X" % (m, o)) if o is not None else str(m)
        print("HITS %s: %d   %s" % (label, counts[va], first.get(va, "")))
    return 0


if __name__ == "__main__":
    sys.exit(main())
