"""V2 P3: safe sweep (flags=0, no ack) of id-1 sub-cases + unmapped core ids.

The client's reliability error path closed the connection when we set the ack flag with a
wrong ack; flags=0 avoids it (verified live: connection stays 24s+). Sweeps and watches:
client packets, KG3D engine-log changes (map loads = enter-world), disconnects.

Usage: python sweep_safe.py
"""
import glob
import os
import struct
import time

GAME_LOG = r"C:\jx3tmp\game_stub_out.txt"
CMD = r"C:\jx3tmp\gsend.hex"
KLOG = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\logs\KG3D_Engine"
OUT = r"C:\jx3tmp\sweep_safe_out.txt"


def log(line):
    with open(OUT, "a", encoding="utf-8") as f:
        f.write(line + "\n")
    print(line, flush=True)


def read(path):
    try:
        return open(path, "rb").read().decode("utf-8", "replace")
    except OSError:
        return ""


def klog_state():
    files = glob.glob(os.path.join(KLOG, "*", "*.log"))
    newest = max(files, key=os.path.getmtime) if files else ""
    try:
        sz = os.path.getsize(newest)
    except OSError:
        sz = 0
    return len(files), newest, sz


def inject(payload):
    open(CMD, "w").write(payload.hex())


def frame(pid, size, sub=None, flags=0, serial=1):
    p = struct.pack("<H", pid) + bytes([flags]) + struct.pack("<H", serial) + struct.pack("<H", 0)
    if sub is None:
        p += struct.pack("<I", 0)
        p += b"\x00" * max(0, size - len(p))
    else:
        p += struct.pack("<H", size) + bytes([sub])
        p += b"\x00" * max(0, size - len(p))
    return p


def main():
    glog = read(GAME_LOG)
    lc = glog.rfind("GAME CONNECT")
    le = max(glog.rfind("error after"), glog.rfind("CLOSE by peer"))
    if not (lc >= 0 and lc > le):
        log("no live game session")
        return 1
    log("=== safe sweep start ===")
    nk, nf, nsz = klog_state()
    base = len(glog)
    for sub in range(1, 61):
        inject(frame(1, 40, sub=sub))
        time.sleep(2.0)
        new = read(GAME_LOG)[base:]
        base = len(read(GAME_LOG))
        k, f, sz = klog_state()
        interesting = [l for l in new.splitlines() if "RECV proto=" in l and "proto=6 " not in l]
        disc = "error after" in new or "CLOSE by peer" in new
        kmark = ""
        if k > nk:
            kmark = " NEW-ENGINE-SESSION(%d)" % k
            nk = k
        if sz > nsz:
            kmark += " KLOG+%d" % (sz - nsz)
            nsz = sz
        if disc:
            log("sub=%-3d DISCONNECT" % sub)
            return 2
        if interesting or kmark:
            log("sub=%-3d %s%s" % (sub, " | ".join(interesting), kmark))
        else:
            nsz = sz
    log("id-1 sweep done, no reaction")
    # unmapped core ids with zero content
    for pid, size in ((2, 15), (3, 11), (9, 16), (14, 19), (17, 43), (18, 209), (21, 19),
                      (23, 49), (25, 52), (27, 13), (31, 40), (33, 19), (35, 23), (42, 9),
                      (48, 31), (51, 47), (54, 12)):
        inject(frame(pid, size))
        time.sleep(2.0)
        new = read(GAME_LOG)[base:]
        base = len(read(GAME_LOG))
        k, f, sz = klog_state()
        interesting = [l for l in new.splitlines() if "RECV proto=" in l and "proto=6 " not in l]
        disc = "error after" in new or "CLOSE by peer" in new
        kmark = ""
        if k > nk:
            kmark = " NEW-ENGINE-SESSION(%d)" % k
            nk = k
        if sz > nsz:
            kmark += " KLOG+%d" % (sz - nsz)
            nsz = sz
        if disc:
            log("id=%-3d DISCONNECT" % pid)
            return 2
        if interesting or kmark:
            log("id=%-3d %s%s" % (pid, " | ".join(interesting), kmark))
        else:
            nsz = sz
    log("core-id sweep done, no reaction")
    return 0


if __name__ == "__main__":
    import sys
    sys.exit(main())
