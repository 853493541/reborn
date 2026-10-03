"""V2 log visibility probe: make the real client's own log stream observable.

Spawns JX3ClientX64.exe suspended, writes the launch block, resumes, then (before the
game.startup group at ~1.9 s) patches *our own probe child only*:

  1. every UTF-16 engine-root string  C:\\SeasunGame\\Game\\JX3\\bin\\zhcn_hd  ->
     C:\\jx3t\\  so that  <root>bin64\\xlogv.exe  resolves to our viewer
     (C:\\jx3t\\bin64\\xlogv.exe; the client's append is a fixed 16-byte copy, so the
     root itself is the only knob - see JX3_CLIENT_LAUNCH_AND_SESSION.md sec.26);
  2. KJX3ConfigModule flag +0x224 = 1  (enables KJX3ConsoleModule::OnInitialize);
  3. Engine log flags [Engine+0x174020] |= 0x6.

Then it watches for the spawned viewer and the growth of C:\\jx3tmp\\client_log.txt.
Transient in-memory patches of our own probe child only - no disk writes, no install
modification (same patch class as doc sec.26).
"""
import ctypes
import ctypes.wintypes as w
import os
import struct
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from probe_state_timeline import (
    EXE, CWD, k32, BLOCK, NAME_FMT, INVALID_HANDLE_VALUE, FILE_MAP_ALL_ACCESS,
    CREATE_SUSPENDED, STARTUPINFO, PROCESS_INFORMATION, build_block, module_list,
)

OPEN_RIGHTS = 0x400 | 0x10 | 0x20 | 0x8          # QUERY | VM_READ | VM_WRITE | VM_OP
ROOT_STR = "C:\\SeasunGame\\Game\\JX3\\bin\\zhcn_hd"
ROOT_WIDE = ROOT_STR.encode("utf-16-le")
NEW_WIDE = "C:\\jx3t\\".encode("utf-16-le")
LOG_FILE = r"C:\jx3tmp\client_log.txt"
EV_ID = os.environ.get("EV_ID", "")
SCAN_START = 1.0
PATCH_AT = 1.78
RUN_UNTIL = 3.6

MEM_COMMIT = 0x1000
PAGE_READABLE = {0x02, 0x04, 0x08, 0x20, 0x40, 0x80}
PAGE_GUARD = 0x100
PAGE_NOACCESS = 0x01
CHUNK = 4 << 20
MAX_SCAN = 1 << 30

k32.VirtualQueryEx.restype = ctypes.c_size_t
k32.VirtualQueryEx.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t]
k32.WriteProcessMemory.restype = w.BOOL
k32.WriteProcessMemory.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t,
                                   ctypes.POINTER(ctypes.c_size_t)]
k32.VirtualProtectEx.restype = w.BOOL
k32.VirtualProtectEx.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_size_t, w.DWORD,
                                 ctypes.POINTER(w.DWORD)]


class MBI(ctypes.Structure):
    _fields_ = [("BaseAddress", ctypes.c_void_p), ("AllocationBase", ctypes.c_void_p),
                ("AllocationProtect", w.DWORD), ("RegionSize", ctypes.c_size_t),
                ("State", w.DWORD), ("Protect", w.DWORD), ("Type", w.DWORD)]


def read_mem(h, addr, size):
    buf = ctypes.create_string_buffer(size)
    n = ctypes.c_size_t()
    if k32.ReadProcessMemory(h, ctypes.c_void_p(addr), buf, size, ctypes.byref(n)) and n.value == size:
        return buf.raw
    return None


def write_mem(h, addr, data):
    old = w.DWORD()
    k32.VirtualProtectEx(h, ctypes.c_void_p(addr), len(data), 0x04, ctypes.byref(old))
    n = ctypes.c_size_t()
    ok = k32.WriteProcessMemory(h, ctypes.c_void_p(addr), data, len(data), ctypes.byref(n)) and n.value == len(data)
    k32.VirtualProtectEx(h, ctypes.c_void_p(addr), len(data), old.value, ctypes.byref(old))
    return ok


def write_u32(h, addr, val):
    return write_mem(h, addr, struct.pack("<I", val))


def read_u64(h, addr):
    d = read_mem(h, addr, 8)
    return struct.unpack("<Q", d)[0] if d else None


def scan_root(h):
    hits = []
    addr = 0
    scanned = 0
    while addr < 0x7FFFFFFFFFFF and scanned < MAX_SCAN:
        mbi = MBI()
        if not k32.VirtualQueryEx(h, ctypes.c_void_p(addr), ctypes.byref(mbi), ctypes.sizeof(mbi)):
            break
        base = mbi.BaseAddress or 0
        size = mbi.RegionSize
        prot = mbi.Protect & 0xFF
        if (mbi.State == MEM_COMMIT and prot in PAGE_READABLE
                and not (mbi.Protect & PAGE_GUARD) and prot != PAGE_NOACCESS):
            off = 0
            carry = b""
            while off < size and scanned < MAX_SCAN:
                n = min(CHUNK, size - off)
                data = read_mem(h, base + off, n)
                if data:
                    scanned += n
                    buf = carry + data
                    start = 0
                    while True:
                        i = buf.find(ROOT_WIDE, start)
                        if i < 0:
                            break
                        va = base + off - len(carry) + i
                        end = i
                        while end + 1 < len(buf) and buf[end:end + 2] != b"\x00\x00":
                            end += 2
                        try:
                            s = buf[i:end].decode("utf-16-le")
                        except Exception:
                            s = "<decode err>"
                        # exact root only: never rewrite a longer path that merely
                        # starts with the root (PATH env, exe/module paths, ...)
                        if s.rstrip("\\") == ROOT_STR.rstrip("\\") and len(s) < 100:
                            hits.append((va, end - i, s))
                        start = i + 2
                    carry = data[-64:]
                off += n
        addr = base + size
    return hits, scanned


def process_names():
    TH32CS_SNAPPROCESS = 0x2

    class PROCESSENTRY32(ctypes.Structure):
        _fields_ = [("dwSize", w.DWORD), ("cntUsage", w.DWORD), ("th32ProcessID", w.DWORD),
                    ("th32DefaultHeapID", ctypes.POINTER(ctypes.c_ulong)), ("th32ModuleID", w.DWORD),
                    ("cntThreads", w.DWORD), ("th32ParentProcessID", w.DWORD), ("pcPriClassBase", ctypes.c_long),
                    ("dwFlags", w.DWORD), ("szExeFile", ctypes.c_char * 260)]

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


class SECURITY_ATTRIBUTES(ctypes.Structure):
    _fields_ = [("nLength", w.DWORD), ("lpSecurityDescriptor", ctypes.c_void_p),
                ("bInheritHandle", w.BOOL)]


def main():
    si = STARTUPINFO()
    si.cb = ctypes.sizeof(STARTUPINFO)
    # capture the engine's early stdout (before the console module takes over):
    # the child must inherit an explicit stdout handle, else those writes are lost
    sa = SECURITY_ATTRIBUTES()
    sa.nLength = ctypes.sizeof(SECURITY_ATTRIBUTES)
    sa.bInheritHandle = True
    early = r"C:\jx3tmp\client_early.txt"
    k32.CreateFileW.restype = w.HANDLE
    k32.CreateFileW.argtypes = [w.LPCWSTR, w.DWORD, w.DWORD, ctypes.c_void_p, w.DWORD, w.DWORD, w.HANDLE]
    hout = k32.CreateFileW(early, 0x40000000, 0x3, ctypes.byref(sa), 2, 0x80, None)
    si.dwFlags = 0x100  # STARTF_USESTDHANDLES
    si.hStdOutput = hout
    si.hStdError = hout
    si.hStdInput = hout
    pi = PROCESS_INFORMATION()
    k32.CreateProcessW(EXE, None, None, None, True, CREATE_SUSPENDED, None, CWD,
                       ctypes.byref(si), ctypes.byref(pi))
    k32.CloseHandle(hout)
    pid = pi.dwProcessId
    h = k32.OpenProcess(OPEN_RIGHTS, False, pid)
    print("pid=%d handle=%d" % (pid, h))
    if not h:
        print("OpenProcess failed err=%d" % k32.GetLastError())
        return 1
    # pre-resume exe patches: the module event calls happen ~0.5 s and race the loop's
    # first module enumeration, so patch the exe while the child is still suspended
    exe_base0 = None
    for _ in range(25):
        for base, size, nm, path in module_list(pid):
            if nm.lower().startswith("jx3client"):
                exe_base0 = base
                break
        if exe_base0:
            break
        time.sleep(0.02)
    if exe_base0:
        if EV_ID:
            ok1 = write_mem(h, exe_base0 + 0xA595B, bytes([int(EV_ID)]))
        else:
            ok1 = write_mem(h, exe_base0 + 0xA5959, b"\x90" * 9)
        ok2 = write_mem(h, exe_base0 + 0xA5969, b"\x90" * 13)
        ok3 = write_mem(h, exe_base0 + 0x955228, b"\\bin64\\lv.exe" + b"\x00" * 3)
        print("pre-resume exe patches (base=0x%X): event=%s flag=%s literal=%s"
              % (exe_base0, ok1, ok2, ok3))
    name = (NAME_FMT % pid).encode()
    hmap = k32.CreateFileMappingA(INVALID_HANDLE_VALUE, None, 4, 0, BLOCK, name)
    p = k32.MapViewOfFile(hmap, FILE_MAP_ALL_ACCESS, 0, 0, BLOCK)
    ctypes.memmove(p, bytes(build_block()), BLOCK)
    k32.UnmapViewOfFile(p)
    k32.ResumeThread(pi.hThread)
    t0 = time.time()

    hits = []
    exe_base = None
    eng_base = None
    patched = False
    viewer_seen = set()
    dumped_rootfn = False
    ring_done = False
    globals_logged = False
    moddump_done = False
    early_done = False
    rootbuf_last = [None]
    log_size_last = [0]
    while time.time() - t0 < RUN_UNTIL:
        el = time.time() - t0
        if k32.WaitForSingleObject(pi.hProcess, 0) == 0:
            print("[%.2f] child EXITED" % el)
            break
        if exe_base is None and el > 0.15:
            for base, size, nm, path in module_list(pid):
                low = nm.lower()
                if low.startswith("jx3client"):
                    exe_base = base
                elif low.startswith("engine_lua5x64"):
                    eng_base = base
        if el >= 0.2 and os.path.exists(LOG_FILE):
            sz = os.path.getsize(LOG_FILE)
            if sz != log_size_last[0]:
                log_size_last[0] = sz
                print("[%.2f] log size=%d" % (el, sz))
        if not dumped_rootfn and el >= 0.9 and exe_base:
            dumped_rootfn = True
            fn = read_u64(h, exe_base + 0x7B84B8)
            print("[%.2f] g_GetRootPath IAT [exe+0x7B84B8] = 0x%X" % (el, fn or 0))
            if fn:
                code = read_mem(h, fn, 160)
                try:
                    from capstone import Cs, CS_ARCH_X86, CS_MODE_64
                    from capstone.x86 import X86_OP_MEM, X86_REG_RIP
                    md = Cs(CS_ARCH_X86, CS_MODE_64)
                    md.detail = True
                    for ins in md.disasm(code, fn):
                        extra = ""
                        for op in ins.operands:
                            if op.type == X86_OP_MEM and op.mem.base == X86_REG_RIP:
                                tgt = ins.address + ins.size + op.mem.disp
                                d = read_mem(h, tgt, 32)
                                extra += " ->0x%X %s" % (tgt, d.hex() if d else "?")
                        print("   0x%X: %s %s%s" % (ins.address, ins.mnemonic, ins.op_str, extra))
                except Exception as e:
                    print("   disasm failed: %s" % e)
        if eng_base and el >= 0.95:
            rb = read_mem(h, eng_base + 0x170060, 64)
            if rb:
                s = rb.split(b"\x00")[0].decode("latin-1")
                if s != rootbuf_last[0]:
                    rootbuf_last[0] = s
                    print("[%.2f] engine ANSI root = %r" % (el, s))
        if not hits and el >= SCAN_START:
            hits, scanned = scan_root(h)
            print("[%.2f] root scan: %d hit(s), %.0f MB scanned" % (el, len(hits), scanned / 1048576.0))
            for va, ln, s in hits[:8]:
                print("   0x%X len=%d  %r" % (va, ln, s))
        # early window: module Initialize runs inside PlatformLoad (0.3-1.86 s per the
        # timeline doc), so the config flag + engine root must be in place well before
        # the 1.9 s game.startup group - and kept applied (the engine may rewrite them)
        if not early_done and exe_base and eng_base:
            early_done = True
            print("[%.2f] EARLY patch (exe=0x%X eng=0x%X)" % (el, exe_base, eng_base))
        if early_done and el < 1.6:
            cm = read_u64(h, exe_base + 0xA8C1C8)
            if cm:
                fl = read_mem(h, cm + 0x224, 4)
                if fl != b"\x01\x00\x00\x00":
                    write_u32(h, cm + 0x224, 1)
            # reachability test: the fixed 16-byte viewer name -> \bin64\lv.exe
            # (we control C:\jx3t\bin64\lv.exe); if OpenXLogV runs, lv.exe spawns
            lit = read_mem(h, exe_base + 0x955228, 16)
            if lit and not lit.startswith(b"\\bin64\\lv.exe"):
                ok = write_mem(h, exe_base + 0x955228, b"\\bin64\\lv.exe" + b"\x00" * 3)
                print("[%.2f] literal -> \\bin64\\lv.exe %s" % (el, "ok" if ok else "FAIL"))
            # OnInitialize gates on edx == 1 (Initialize); the probe's dispatch calls the
            # module event handler with other event ids only, so NOP the event-id check:
            # then the full OnInitialize runs on any event (OpenXLogV is idempotent via
            # module+0x18) and its success path installs the log sink -> viewer gets data.
            if EV_ID:
                # keep the cmp edx, imm; only the immediate is patched: the viewer
                # spawns iff the dispatched event id equals EV_ID
                ev = read_mem(h, exe_base + 0xA595B, 1)
                if ev != bytes([int(EV_ID)]):
                    ok = write_mem(h, exe_base + 0xA595B, bytes([int(EV_ID)]))
                    print("[%.2f] event-id immediate -> %s %s" % (el, EV_ID, "ok" if ok else "FAIL"))
            else:
                ev = read_mem(h, exe_base + 0xA5959, 9)
                if ev and ev != b"\x90" * 9:
                    ok = write_mem(h, exe_base + 0xA5959, b"\x90" * 9)
                    print("[%.2f] OnInitialize event-id check NOPed %s" % (el, "ok" if ok else "FAIL"))
            # also NOP the config-flag check (13 bytes at 0xA5969) - the event calls race
            # the flag write, and the flag is only a policy gate for the log viewer
            fc = read_mem(h, exe_base + 0xA5969, 13)
            if fc and fc != b"\x90" * 13:
                ok = write_mem(h, exe_base + 0xA5969, b"\x90" * 13)
                print("[%.2f] OnInitialize flag check NOPed %s" % (el, "ok" if ok else "FAIL"))
            rb = read_mem(h, eng_base + 0x170060, 16)
            if rb and not rb.startswith(b"C:\\jx3t\\"):
                write_mem(h, eng_base + 0x170060, b"C:\\jx3t\\\x00")
                print("[%.2f] re-applied engine root (was %r)" % (el, rb.split(b"\x00")[0].decode("latin-1")))
        if not patched and el >= PATCH_AT:
            patched = True
            print("[%.2f] patching root + flags (exe=0x%X eng=0x%X)" % (el, exe_base or 0, eng_base or 0))
            for va, ln, s in hits:
                new = NEW_WIDE + b"\x00" * (ln - len(NEW_WIDE)) if ln >= len(NEW_WIDE) else NEW_WIDE
                ok = write_mem(h, va, new)
                chk = read_mem(h, va, len(new))
                print("   root 0x%X -> %s (%s)" % (va, "ok" if ok else "FAIL",
                      chk[:32].decode("utf-16-le", "replace") if chk else "?"))
            if exe_base:
                cm = read_u64(h, exe_base + 0xA8C1C8)
                print("   configModule=0x%X" % (cm or 0))
                if cm:
                    print("   config +0x224 was %s -> write %s" % (
                        read_mem(h, cm + 0x224, 4).hex() if read_mem(h, cm + 0x224, 4) else "?",
                        "ok" if write_u32(h, cm + 0x224, 1) else "FAIL"))
            if eng_base:
                fl = read_mem(h, eng_base + 0x174020, 4)
                cur = struct.unpack("<I", fl)[0] if fl else 0
                print("   engine flags 0x%X | 0x6 -> %s" % (cur, "ok" if write_u32(h, eng_base + 0x174020, cur | 6) else "FAIL"))
                # KGLogPrintf console path: the GetFileType check uses a handle cached
                # from the first call (pre-dup2), so the type never matches - NOP the
                # jne at 0xE550F (6 bytes) so the console fputs path always runs
                jb = read_mem(h, eng_base + 0xE550F, 6)
                if jb and jb != b"\x90" * 6:
                    ok = write_mem(h, eng_base + 0xE550F, b"\x90" * 6)
                    print("   KGLog type-check NOPed %s" % ("ok" if ok else "FAIL"))
                # the engine's g_GetRootPath copies this ANSI buffer (verified: it is
                # ANSI, not UTF-16 - the earlier UTF-16-only patch missed it)
                rb = read_mem(h, eng_base + 0x170060, 64)
                cur_root = rb.split(b"\x00")[0].decode("latin-1") if rb else "?"
                ok = write_mem(h, eng_base + 0x170060, b"C:\\jx3t\\\x00")
                chk = read_mem(h, eng_base + 0x170060, 16)
                print("   engine ANSI root %r -> %s (%r)" % (
                    cur_root, "ok" if ok else "FAIL",
                    chk.split(b"\x00")[0].decode("latin-1") if chk else "?"))
        if eng_base and not globals_logged and el >= 1.5:
            globals_logged = True
            for off, nm in ((0x177598, "sink"), (0x174028, "gate174028"), (0x170440, "mask"),
                            (0x174020, "flags"), (0x174430, "ring_ptr")):
                v = read_mem(h, eng_base + off, 8)
                print("[%.2f] engine %s @+0x%X = %s" % (el, nm, off, v.hex() if v else "?"))
        if eng_base and not ring_done and el >= 2.02:
            ring_done = True
            rp = read_u64(h, eng_base + 0x174430)
            if rp:
                lb = read_mem(h, rp + 4, 4)
                n = struct.unpack("<I", lb)[0] if lb else 0
                if 0 < n < 0x40000:
                    data = read_mem(h, rp + 8, n)
                    if data:
                        print("=== engine KGLog ring (%d bytes) ===" % n)
                        print(data.decode("gb18030", "replace")[:12000])
                else:
                    print("ring ptr=0x%X len=%d" % (rp, n))
        if patched and not moddump_done and el >= 2.05:
            moddump_done = True
            print("[%.2f] module globals (RTTI-resolved):" % el)
            for g in range(0xA8C180, 0xA8C2A0, 8):
                gp = read_u64(h, exe_base + g)
                if not gp:
                    continue
                vt = read_u64(h, gp)
                nm = "?"
                if vt:
                    col = read_u64(h, vt - 8)
                    if col:
                        td = read_u64(h, col + 0x10)
                        if td:
                            raw = read_mem(h, td + 0x10, 96)
                            if raw:
                                nm = raw.split(b"\x00")[0].decode("latin-1", "replace")
                extra = ""
                if "Console" in nm:
                    extra = " +0x18=%s" % (read_mem(h, gp + 0x18, 8).hex() if read_mem(h, gp + 0x18, 8) else "?")
                print("   0x%X -> 0x%X %s%s" % (g, gp, nm, extra))
            if exe_base:
                cm = read_u64(h, exe_base + 0xA8C1C8)
                if cm:
                    fl = read_mem(h, cm + 0x224, 4)
                    print("   config +0x224 now = %s" % (fl.hex() if fl else "?"))
        if patched:
            for ppid, par, nm in process_names():
                low = nm.lower()
                if low in ("xlogv.exe", "lv.exe") and ppid not in viewer_seen:
                    viewer_seen.add(ppid)
                    print("[%.2f] viewer spawned: %s pid=%d parent=%d" % (el, nm, ppid, par))
            if os.path.exists(LOG_FILE):
                sz = os.path.getsize(LOG_FILE)
                if sz:
                    print("[%.2f] %s size=%d" % (el, LOG_FILE, sz))
        time.sleep(0.05)

    time.sleep(1.0)
    # the viewer can hold the inherited write end and block forever - kill our viewers
    for ppid, par, nm in process_names():
        if nm.lower() in ("lv.exe", "xlogv.exe") and par == pid:
            hp = k32.OpenProcess(1, False, ppid)
            if hp:
                k32.TerminateProcess(hp, 1)
    if os.path.exists(LOG_FILE):
        data = open(LOG_FILE, "rb").read()
        print("=== client_log.txt: %d bytes ===" % len(data))
        print(data[:4000].decode("gb18030", "replace"))
    else:
        print("client_log.txt not created")
    return 0


if __name__ == "__main__":
    sys.exit(main())
