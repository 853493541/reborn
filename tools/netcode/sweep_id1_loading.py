"""V2 P3: find the id-1 sub-case that triggers the loading/enter-world.

Re-establishes the game session (post_login + role_enter), then injects id-1 sub-cases
and watches for: a NEW KG3D engine log session (the loading/engine re-init signature),
new client packets, or a disconnect. The sub that starts a new engine session is the
handshake-respond / enter-world trigger.

Usage: python sweep_id1_loading.py
"""
import glob
import os
import struct
import subprocess
import time

VENV = r"C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe"
TOOLS = r"C:\Users\Zhibin Ren\Desktop\reborn-iso-v2\tools\netcode"
GW_LOG = r"C:\jx3tmp\gw_stdout.txt"
GAME_LOG = r"C:\jx3tmp\game_stub_out.txt"
CMD = r"C:\jx3tmp\gsend.hex"
KLOG = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\logs\KG3D_Engine\2026_10_05"
OUT = r"C:\jx3tmp\sweep_loading_log.txt"


def log(line):
    with open(OUT, "a", encoding="utf-8") as f:
        f.write(line + "\n")
    print(line, flush=True)


def read(path):
    try:
        return open(path, "rb").read().decode("utf-8", "replace")
    except OSError:
        return ""


def klog_count():
    return len(glob.glob(os.path.join(KLOG, "*.log")))


def session_live():
    glog = read(GAME_LOG)
    last_conn = glog.rfind("GAME CONNECT")
    last_end = max(glog.rfind("error after"), glog.rfind("CLOSE by peer"))
    return last_conn >= 0 and last_conn > last_end


def ensure_session():
    if session_live():
        return True
    for attempt in range(6):
        subprocess.run([VENV, "-u", os.path.join(TOOLS, "post_login.py")],
                       capture_output=True, timeout=60)
        if "RECV proto=3 " in read(GW_LOG):
            subprocess.run([VENV, "-u", os.path.join(TOOLS, "role_enter.py")],
                           capture_output=True, timeout=90)
            time.sleep(6)
            if session_live():
                return True
        time.sleep(8)
    return False


def main():
    log("=== loading sweep start ===")
    while True:
        if not ensure_session():
            log("no session; retry in 30s")
            time.sleep(30)
            continue
        base_log = len(read(GAME_LOG))
        base_k = klog_count()
        log("session ready (klogs=%d); sweeping subs 2..60" % base_k)
        for sub in range(2, 61):
            size = 40
            payload = (struct.pack("<H", 1) + b"\x02" + struct.pack("<H", 0)
                       + struct.pack("<H", 1) + struct.pack("<H", size) + bytes([sub]))
            payload += b"\x00" * (size - len(payload))
            open(CMD, "w").write(payload.hex())
            time.sleep(3.0)
            new = read(GAME_LOG)[base_log:]
            base_log = len(read(GAME_LOG))
            k = klog_count()
            interesting = [l for l in new.splitlines() if "RECV proto=" in l and "proto=6 " not in l]
            disc = "error after" in new or "CLOSE by peer" in new
            if k > base_k:
                log("sub=%-3d  <<< NEW ENGINE SESSION (klogs %d->%d) %s" % (sub, base_k, k, " | ".join(interesting)))
                base_k = k
                time.sleep(20)
            elif disc:
                log("sub=%-3d  disconnect %s" % (sub, " | ".join(interesting)))
                break
            elif interesting:
                log("sub=%-3d  packets: %s" % (sub, " | ".join(interesting)))
        time.sleep(15)


if __name__ == "__main__":
    main()
