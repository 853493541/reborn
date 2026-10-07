"""V2: watch the JX3Represent singleton avatar array for duplicate entries (live).

The 0xC0000374 crash is an LFH double-free in ~KGameWorldHandler teardown: the global
array at JX3RepresentX64.dll+0xF51298 holds the same heap block twice (proven from the
caught dump `1791356323_...dmp`: heap_failure_lfh_bitmap_corruption at block
0x1E3E9D06E00 = element 0x1E3E9D06E10, array indices 8 and 9).

This watcher polls that array on the live raw client and prints the element count and
duplicate indices whenever they change, so the exact trigger (a stub packet, the engine
scene load, ...) can be correlated with the stub log timestamps.

Usage: python watch_represent_array.py [--interval 2] [--log C:\\jx3tmp\\rep_array.txt]
"""
import ctypes
import os
import struct
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import watch_world_bind as W

SINGLETON_RVA = 0xF51298


def qwords(reader, addr, n):
    d = reader.mem(addr, 8 * n)
    if not d:
        return None
    return list(struct.unpack("<%dQ" % n, d))


def snapshot(reader, base):
    obj = reader.u64(base + SINGLETON_RVA)
    if not obj:
        return None
    begin = reader.u64(obj + 0x10)
    end = reader.u64(obj + 0x18)
    if begin is None or end is None or end < begin or (end - begin) > 0x8000:
        return None
    n = (end - begin) // 8
    vals = qwords(reader, begin, n) if n else []
    if vals is None:
        return None
    return obj, begin, n, vals


def main():
    interval = 2.0
    logpath = r"C:\jx3tmp\rep_array.log"
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--interval" and i + 1 < len(args):
            interval = float(args[i + 1])
        elif a == "--log" and i + 1 < len(args):
            logpath = args[i + 1]
    lf = open(logpath, "a", encoding="utf-8")

    def w(line):
        lf.write(line + "\n")
        lf.flush()
        print(line, flush=True)

    pid = W.find_client_pid()
    if not pid:
        w("no client")
        return 1
    base = W.module_base(pid, "jx3representx64")
    if not base:
        w("no JX3RepresentX64 module in pid=%s" % pid)
        return 1
    hp = ctypes.windll.kernel32.OpenProcess(0x438, False, pid)
    reader = W.Reader(hp)
    w("watching pid=%d JX3Represent base=0x%X singleton=0x%X interval=%.1fs" % (
        pid, base, base + SINGLETON_RVA, interval))
    last = None
    misses = 0
    while True:
        if W.find_client_pid() != pid:
            misses += 1
            if misses >= 5:
                w("[%s] client exited" % time.strftime("%H:%M:%S"))
                return 0
        else:
            misses = 0
        reader.clear()
        try:
            snap = snapshot(reader, base)
        except Exception as e:
            snap = None
            w("[%s] read error: %s" % (time.strftime("%H:%M:%S"), e))
        if snap is not None:
            obj, begin, n, vals = snap
            seen = {}
            dups = []
            for i, v in enumerate(vals):
                if v in seen:
                    dups.append((seen[v], i, v))
                else:
                    seen[v] = i
            sig = (n, tuple((a, b, hex(v)) for a, b, v in dups))
            if sig != last:
                w("[%s] obj=0x%X elems=%d dups=%s" % (
                    time.strftime("%H:%M:%S"), obj, n,
                    ", ".join("#%d=#%d(0x%X)" % (a, b, v) for a, b, v in dups) or "none"))
                last = sig
        time.sleep(interval)


if __name__ == "__main__":
    sys.exit(main())
