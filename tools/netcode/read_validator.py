"""Live read of the position-validator inputs (0x1403D5220).

Reads: cell sub-region array [cell+0x20], index table [cell+0x28], the record for
idx=(minorY<<6)+minorX, its flags/height words, and compares with the player Z.
Usage: python read_validator.py [samples]
"""
import ctypes
import struct
import sys
import time

sys.path.insert(0, r"C:\Users\Zhibin Ren\Desktop\reborn-iso-v2\tools\netcode")
import watch_world_bind as W

k32 = ctypes.windll.kernel32


def main():
    samples = int(sys.argv[1]) if len(sys.argv) > 1 else 1
    pid = W.find_client_pid()
    if not pid:
        print("no client")
        return 1
    base = W.module_base(pid, "jx3client")
    hp = k32.OpenProcess(0x438, False, pid)
    for _ in range(samples):
        r = W.Reader(hp)
        info = W.snapshot(r, base)
        player, scene = info.get("player"), info.get("p60")
        px, py, pz = info.get("px"), info.get("py"), info.get("pz")
        minor = info.get("minor")
        print("pid=%s state=%s pos=(%s,%s,%s) minor=%s scene=0x%X" % (
            pid, info.get("state"), px, py, pz, minor, scene or 0))
        if player and scene and minor:
            cx, cy = (px or 0) >> 11, (py or 0) >> 11
            cell = r.u64(scene + 0x7A8 + (cy * 128 + cx) * 8)
            stored = (r.i32(scene + 0x20DCC), r.i32(scene + 0x20DD0))
            print("  cell_origin=(%d,%d) stored=%s cell=0x%X" % (cx, cy, stored, cell or 0))
            if cell:
                arr = r.u64(cell + 0x20)
                idx_tab = r.u64(cell + 0x28)
                idx = (minor[1] << 6) + minor[0]
                entry = r.i32(idx_tab + idx * 4) if idx_tab else None
                print("  arr=0x%X idx_tab=0x%X idx=%d entry=%s" % (arr or 0, idx_tab or 0, idx, entry))
                if arr and entry is not None and -0x10000 < entry < 0x10000:
                    rec = arr + entry * 8
                    flags = r.u32(rec)
                    hb = r.mem(rec + 4, 4)
                    h1 = struct.unpack("<H", hb[0:2])[0] if hb else None
                    h2 = struct.unpack("<H", hb[2:4])[0] if hb else None
                    zc = h1 if (flags & 1) else h2
                    print("  rec=0x%X flags=0x%X h_a=%d h_b=%d chosen_lo=%d loZ=%d hiZ=%d playerZ=%s" % (
                        rec, flags or 0, h1 or 0, h2 or 0, zc or 0,
                        ((zc or 0) << 6), ((h1 or 0) << 6), pz))
        time.sleep(4)
    return 0


if __name__ == "__main__":
    sys.exit(main())
