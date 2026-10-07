"""Ad-hoc: read the KSceneClientLoader state on the live client.

scene+0x211D8 = KSceneClientLoader; fields seen in disasm:
  +0x20  byte  active flag (set by Init)
  +0x140 u32   request seq counter (incremented by the request appender 0x1403D8A00)
  +0x150 u32   pending-request count (incremented by appender, decremented by consumer)
  +0x154 u32   loaded-region count (incremented by consumer when a region is added)
  +0x30  u64   request queue size?
  +0xD8  u64   loaded-region list size?
Usage: python read_scene_loader.py [samples] [interval]
"""
import ctypes
import struct
import sys
import time

sys.path.insert(0, r"C:\Users\Zhibin Ren\Desktop\reborn-iso-v2\tools\netcode")
import watch_world_bind as W

k32 = ctypes.windll.kernel32


def main():
    samples = int(sys.argv[1]) if len(sys.argv) > 1 else 3
    interval = float(sys.argv[2]) if len(sys.argv) > 2 else 5.0
    pid = W.find_client_pid()
    if not pid:
        print("no client")
        return 1
    base = W.module_base(pid, "jx3client")
    hp = k32.OpenProcess(0x438, False, pid)
    r = W.Reader(hp)
    client = r.u64(base + 0xA755B8)
    reg = r.u64(client + 0x5673D8) if client else None
    print("pid=%s base=0x%X client=0x%X reg=0x%X" % (pid, base, client or 0, reg or 0))
    if not reg:
        return 1
    head = reg + 8
    root = r.u64(head + 8)
    scene = r.u64(root + 0x28) if root and root != head else 0
    print("scene head=0x%X root=0x%X scene=0x%X" % (head, root or 0, scene or 0))
    if not scene:
        return 1
    loader = scene + 0x211D8
    for i in range(samples):
        r.clear()
        vals = {
            "active": r.u8(loader + 0x20),
            "seq": r.u32(loader + 0x140),
            "pending": r.u32(loader + 0x150),
            "regions": r.u32(loader + 0x154),
            "qsize": r.u64(loader + 0x30),
            "reglist": r.u64(loader + 0xD8),
        }
        print("[%s] active=%s seq=%s pending=%s regions=%s qsize=%s reglist=%s" % (
            time.strftime("%H:%M:%S"), vals["active"], vals["seq"], vals["pending"],
            vals["regions"], vals["qsize"], vals["reglist"]))
        time.sleep(interval)
    return 0


if __name__ == "__main__":
    sys.exit(main())
