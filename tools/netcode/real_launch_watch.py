"""V2 P1.2: read-only observation of a real client launch.

Waits for a JX3ClientX64.exe process, then polls the system TCP table
(GetExtendedTcpTable / TCP_TABLE_OWNER_PID_ALL) for that PID and logs every new
connection (local/remote addr:port, state) plus the process tree, for N seconds.
No packet capture, no injection - just connection-table reads.
"""
import ctypes
import ctypes.wintypes as w
import socket
import struct
import sys
import time

iphlp = ctypes.windll.iphlpapi
k32 = ctypes.windll.kernel32
AF_INET = 2
TCP_TABLE_OWNER_PID_ALL = 5
EXE_NAME = "jx3clientx64.exe"


class MIB_TCPROW_OWNER_PID(ctypes.Structure):
    _fields_ = [("dwState", w.DWORD), ("dwLocalAddr", w.DWORD), ("dwLocalPort", w.DWORD),
                ("dwRemoteAddr", w.DWORD), ("dwRemotePort", w.DWORD), ("dwOwningPid", w.DWORD)]


def tcp_rows():
    size = w.DWORD(0)
    iphlp.GetExtendedTcpTable(None, ctypes.byref(size), False, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0)
    buf = ctypes.create_string_buffer(size.value)
    r = iphlp.GetExtendedTcpTable(buf, ctypes.byref(size), False, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0)
    if r != 0:
        return []
    n = struct.unpack_from("<I", buf.raw, 0)[0]
    rows = []
    off = 4
    for _ in range(n):
        row = MIB_TCPROW_OWNER_PID.from_buffer_copy(buf.raw[off:off + ctypes.sizeof(MIB_TCPROW_OWNER_PID)])
        rows.append(row)
        off += ctypes.sizeof(MIB_TCPROW_OWNER_PID)
    return rows


def ip(dw):
    return socket.inet_ntoa(struct.pack("<I", dw))


def port(dw):
    p = dw & 0xFFFF
    return ((p >> 8) & 0xFF) | ((p & 0xFF) << 8)


def find_pid():
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


def main():
    wait_s = int(sys.argv[1]) if len(sys.argv) > 1 else 90
    run_s = int(sys.argv[2]) if len(sys.argv) > 2 else 25
    print("waiting up to %ds for %s ..." % (wait_s, EXE_NAME))
    t0 = time.time()
    pid = None
    while time.time() - t0 < wait_s:
        for p, par, nm in find_pid():
            if nm.lower() == EXE_NAME:
                pid = p
                print("[%.1f] found %s pid=%d parent=%d" % (time.time() - t0, nm, p, par))
                break
        if pid:
            break
        time.sleep(0.2)
    if not pid:
        print("no client seen")
        return 1
    seen = set()
    tree_seen = set()
    t1 = time.time()
    while time.time() - t1 < run_s:
        el = time.time() - t1
        for row in tcp_rows():
            if row.dwOwningPid != pid:
                continue
            key = (row.dwState, row.dwLocalAddr, row.dwLocalPort, row.dwRemoteAddr, row.dwRemotePort)
            if key in seen:
                continue
            seen.add(key)
            print("[%.2f] TCP state=%d local=%s:%d remote=%s:%d" % (
                el, row.dwState, ip(row.dwLocalAddr), port(row.dwLocalPort),
                ip(row.dwRemoteAddr), port(row.dwRemotePort)))
        for p, par, nm in find_pid():
            if par == pid and p not in tree_seen:
                tree_seen.add(p)
                print("[%.2f] child %s pid=%d" % (el, nm, p))
        time.sleep(0.1)
    print("done")
    return 0


if __name__ == "__main__":
    sys.exit(main())
