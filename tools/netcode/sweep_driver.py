"""V2 P3: autonomous game-packet sweep driver.

Loops: ensure a game session (post_login + role_enter if needed) -> inject candidate
packets -> detect the client's reaction (new packets / disconnect) -> log -> re-login.

Usage: python sweep_driver.py [--mode id1|core] [--secs 3600]
"""
import os
import struct
import subprocess
import sys
import time

VENV = r"C:\Users\Zhibin Ren\Desktop\reborn\.venv\Scripts\python.exe"
TOOLS = r"C:\Users\Zhibin Ren\Desktop\reborn-iso-v2\tools\netcode"
GW_LOG = r"C:\jx3tmp\gw_stdout.txt"
GAME_LOG = r"C:\jx3tmp\game_stub_out.txt"
CMD = r"C:\jx3tmp\gsend.hex"
OUT = r"C:\jx3tmp\sweep_driver_out.txt"


def log(line):
    with open(OUT, "a", encoding="utf-8") as f:
        f.write(line + "\n")
    print(line, flush=True)


def read_file(path):
    try:
        return open(path, "rb").read().decode("utf-8", "replace")
    except OSError:
        return ""


def run(cmd):
    try:
        return subprocess.run(cmd, capture_output=True, timeout=120)
    except Exception as e:
        return e


def ensure_session():
    """Returns True when a fresh game connection (RECV proto=1) exists."""
    glog = read_file(GAME_LOG)
    if "RECV proto=1 " in glog:
        return True
    for attempt in range(6):
        run([VENV, "-u", os.path.join(TOOLS, "post_login.py")])
        gw = read_file(GW_LOG)
        if "RECV proto=3 " in gw:
            run([VENV, "-u", os.path.join(TOOLS, "role_enter.py")])
            time.sleep(6)
            glog = read_file(GAME_LOG)
            if "RECV proto=1 " in glog:
                return True
        time.sleep(8)
    return False


def sweep_once(mode):
    """Inject one candidate; returns ('ok'|'disconnect'|'noconn', desc)."""
    glog = read_file(GAME_LOG)
    base = len(glog)
    subs = range(1, 61) if mode == "id1" else range(1, 61)
    for sub in subs:
        size = 40
        payload = (struct.pack("<H", 1) + b"\x02" + struct.pack("<H", 0)
                   + struct.pack("<H", 1) + struct.pack("<H", size) + bytes([sub]))
        payload += b"\x00" * (size - len(payload))
        open(CMD, "w").write(payload.hex())
        time.sleep(2.0)
        new = read_file(GAME_LOG)[base:]
        base = len(read_file(GAME_LOG))
        lines = [l for l in new.splitlines() if "RECV proto=" in l]
        interesting = [l for l in lines if "proto=6 " not in l]
        if "error after" in new or "CLOSE by peer" in new:
            return "disconnect", "id1 sub=%d | %s" % (sub, " | ".join(interesting))
        if interesting:
            return "reaction", "id1 sub=%d | %s" % (sub, " | ".join(interesting))
    return "ok", "id1 sweep complete, no reaction"


def main():
    mode = "id1"
    secs = 3600.0
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == "--mode" and i + 1 < len(args):
            mode = args[i + 1]
        elif a == "--secs" and i + 1 < len(args):
            secs = float(args[i + 1])
    log("=== sweep driver start mode=%s ===" % mode)
    t0 = time.time()
    while time.time() - t0 < secs:
        if not ensure_session():
            log("no session after retries; sleeping 30s")
            time.sleep(30)
            continue
        result, desc = sweep_once(mode)
        log("%s: %s" % (result, desc))
        if result in ("disconnect", "reaction"):
            time.sleep(20)
    log("driver done")


if __name__ == "__main__":
    main()
