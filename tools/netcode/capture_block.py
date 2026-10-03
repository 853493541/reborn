"""V2 P1.2: capture the launcher's PID-keyed block write before the client consumes it.

Polls the session BaseNamedObjects directory (NtQueryDirectoryObject) every ~1 ms for a
new 400BBBA7-...-D62109852BD6 mapping, then samples the 0x275C block every ~1 ms and dumps
the first non-zero content with a timestamp. Read-only, no process access.
"""
import ctypes
import ctypes.wintypes as w
import sys
import time

ntdll = ctypes.windll.ntdll
k32 = ctypes.windll.kernel32
k32.OpenFileMappingA.restype = w.HANDLE
k32.OpenFileMappingA.argtypes = [w.DWORD, w.BOOL, ctypes.c_char_p]
k32.MapViewOfFile.restype = ctypes.c_void_p
k32.MapViewOfFile.argtypes = [w.HANDLE, w.DWORD, w.DWORD, w.DWORD, ctypes.c_size_t]
BLOCK = 0x275C
PREFIX = "400BBBA7-F29F-4357-9B07-"
NAME_FMT = "400BBBA7-F29F-4357-9B07-%04X-D62109852BD6"


class UNICODE_STRING(ctypes.Structure):
    _fields_ = [("Length", ctypes.c_ushort), ("MaximumLength", ctypes.c_ushort),
                ("_pad", ctypes.c_ulong), ("Buffer", ctypes.c_void_p)]


class OBJECT_ATTRIBUTES(ctypes.Structure):
    _fields_ = [("Length", ctypes.c_ulong), ("RootDirectory", ctypes.c_void_p),
                ("ObjectName", ctypes.POINTER(UNICODE_STRING)), ("Attributes", ctypes.c_ulong),
                ("SecurityDescriptor", ctypes.c_void_p), ("SecurityQualityOfService", ctypes.c_void_p)]


def open_bno():
    sid = ctypes.c_ulong()
    k32.ProcessIdToSessionId(k32.GetCurrentProcessId(), ctypes.byref(sid))
    for path in ("\\Sessions\\%d\\BaseNamedObjects" % sid.value, "\\BaseNamedObjects"):
        name = UNICODE_STRING()
        buf = ctypes.create_unicode_buffer(path)
        name.Length = len(path) * 2
        name.MaximumLength = name.Length + 2
        name.Buffer = ctypes.cast(buf, ctypes.c_void_p)
        oa = OBJECT_ATTRIBUTES()
        oa.Length = ctypes.sizeof(OBJECT_ATTRIBUTES)
        oa.ObjectName = ctypes.pointer(name)
        oa.Attributes = 0x40  # OBJ_CASE_INSENSITIVE
        h = w.HANDLE()
        st = ntdll.NtOpenDirectoryObject(ctypes.byref(h), 0x1, ctypes.byref(oa)) & 0xFFFFFFFF
        if st == 0:
            print("opened %s" % path, flush=True)
            return h
        print("NtOpenDirectoryObject(%s) st=0x%X" % (path, st), flush=True)
    return None


def dir_names(h):
    out = []
    ctx = ctypes.c_ulong(0)
    buf = ctypes.create_string_buffer(4096)
    ret = ctypes.c_ulong(0)
    restart = True
    pat = PREFIX.encode("utf-16-le")
    while True:
        st = ntdll.NtQueryDirectoryObject(h, buf, 4096, True, restart, ctypes.byref(ctx), ctypes.byref(ret))
        st &= 0xFFFFFFFF
        if st not in (0, 0x80000005):  # STATUS_SUCCESS / STATUS_MORE_ENTRIES
            if restart:
                print("NtQueryDirectoryObject st=0x%X" % st, flush=True)
            break
        restart = False
        raw = buf.raw
        i = raw.find(pat)
        if i >= 0:
            j = i
            while j + 1 < len(raw) and raw[j:j + 2] != b"\x00\x00":
                j += 2
            out.append(raw[i:j].decode("utf-16-le", "replace"))
    return out


def main():
    h = open_bno()
    if h is None:
        return 1
    seen = set()
    for nm in dir_names(h):
        if nm.startswith(PREFIX):
            seen.add(nm)
    print("existing block mappings:", seen, flush=True)
    print("waiting for a new block mapping (launch the game now)...", flush=True)
    t0 = time.time()
    pid = None
    while pid is None:
        for nm in dir_names(h):
            if nm.startswith(PREFIX) and nm not in seen:
                try:
                    pid = int(nm[len(PREFIX):len(PREFIX) + 4], 16)
                except Exception:
                    continue
                break
        if pid is None:
            time.sleep(0.001)
    print("new block for pid=%d at +%.3fs" % (pid, time.time() - t0), flush=True)
    name = (NAME_FMT % pid).encode()
    hm = None
    mapped = None
    last = None
    t1 = time.time()
    while time.time() - t1 < 12:
        if hm is None:
            hm = k32.OpenFileMappingA(0xF001F, False, name)
            if not hm:
                time.sleep(0.0005)
                continue
            mapped = k32.MapViewOfFile(hm, 0xF001F, 0, 0, BLOCK)
            print("mapping opened at +%.4fs" % (time.time() - t1), flush=True)
        data = ctypes.string_at(mapped, BLOCK)
        if data != last:
            nz = [(i, data[i]) for i in range(BLOCK) if data[i]]
            if last is None:
                print("[+%.4fs] FIRST read: %d non-zero bytes" % (time.time() - t1, len(nz)), flush=True)
                for i, b in nz[:128]:
                    print("   +0x%X = 0x%02X" % (i, b), flush=True)
                print("full hex:", data.hex(), flush=True)
            else:
                print("[+%.4fs] change: %d non-zero bytes" % (time.time() - t1, len(nz)), flush=True)
                print("full hex:", data.hex(), flush=True)
            last = data
        time.sleep(0.0005)
    return 0


if __name__ == "__main__":
    sys.exit(main())
