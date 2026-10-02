#!/usr/bin/env python3
"""M2 authoritative-movement smoke: server (heightmap + game speed) + two engine clients.

Drives both clients with a held W (run) for ~10 s each while RC_NET_AUTH=1, then prints
the net lines from both logs (join / entity add / corrections). The server follows the
baked heightfield, so client drift should stay small and corrections rare.

Run:  .venv\\Scripts\\python.exe tools\\proof\\run_m2_auth_smoke.py
"""
import ctypes
import ctypes.wintypes as w
import os
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BIN = Path(r"C:\SeasunGame\MovieEditor\bin64")
OUT = BIN / "reborn_out"
SERVER = ROOT / "netcode" / "Reborn.Server" / "bin" / "Debug" / "net5.0" / "Reborn.Server.exe"
HF = ROOT / "netcode" / "data" / "龙门寻宝_hf.tsv"
SPAWN = "23334,761,24424"
PORT = 5599

u32 = ctypes.windll.user32
u32.GetWindowThreadProcessId.argtypes = [w.HWND, ctypes.POINTER(w.DWORD)]


def procs(name):
    out = subprocess.run(["powershell", "-NoProfile", "-Command",
                          "(Get-CimInstance Win32_Process -Filter \"Name='%s'\" | Select-Object -ExpandProperty ProcessId)" % name],
                         capture_output=True, text=True)
    return [int(x) for x in out.stdout.split()]


def kill(names):
    for n in names:
        for pid in procs(n):
            subprocess.run(["taskkill", "/PID", str(pid), "/F"], capture_output=True)


def find_window(pid):
    best = [0, None]

    @ctypes.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
    def cb(h, l):
        p2 = w.DWORD()
        u32.GetWindowThreadProcessId(h, ctypes.byref(p2))
        if p2.value == pid and u32.IsWindowVisible(h):
            buf = ctypes.create_unicode_buffer(256)
            u32.GetClassNameW(h, buf, 256)
            if buf.value.startswith("WindowsForms"):
                r = w.RECT()
                u32.GetWindowRect(h, ctypes.byref(r))
                area = (r.right - r.left) * (r.bottom - r.top)
                if area > best[0]:
                    best[0] = area
                    best[1] = h
        return True

    u32.EnumWindows(cb, 0)
    return best[1]


def send_key(hwnd, vk, down=True):
    u32.SetForegroundWindow(hwnd)
    time.sleep(0.3)
    u32.PostMessageW(hwnd, 0x0100 if down else 0x0101, vk, 0)


def newest_log(tag):
    best = None
    for f in OUT.glob("reborn*.log"):
        try:
            head = f.read_text(encoding="utf-8", errors="ignore")[:600]
        except Exception:
            continue
        if "build=" + tag in head:
            if best is None or f.stat().st_mtime > best.stat().st_mtime:
                best = f
    return best


def main():
    kill(["reborn_client_m2-a.exe", "reborn_client_m2-b.exe", "Reborn.Server.exe"])
    time.sleep(1)
    env = dict(os.environ)
    env["RC_NET"] = "127.0.0.1:%d" % PORT
    env["RC_NET_AUTH"] = "1"
    env["RC_DEMO"] = "0"
    env["RC_AUTORUN"] = "90000"
    srv = subprocess.Popen([str(SERVER), "--port", str(PORT), "--heightmap", str(HF),
                            "--speed", "320", "--aoi", "5000", "--spawn", SPAWN],
                           cwd=str(ROOT), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    time.sleep(2)
    a = subprocess.Popen([str(BIN / "reborn_client_m2-a.exe")], cwd=r"C:\SeasunGame\MovieEditor", env=env)
    time.sleep(6)
    b = subprocess.Popen([str(BIN / "reborn_client_m2-b.exe")], cwd=r"C:\SeasunGame\MovieEditor", env=env)
    print("server=%d a=%d b=%d" % (srv.pid, a.pid, b.pid))

    t0 = time.time()
    while time.time() - t0 < 40:
        time.sleep(1)
    wa = find_window(a.pid)
    wb = find_window(b.pid)
    print("windows a=%s b=%s" % (wa, wb))
    if wa:
        send_key(wa, 0x57, True)
        print("[%.0fs] A W down" % (time.time() - t0))
    time.sleep(10)
    if wa:
        send_key(wa, 0x57, False)
        print("[%.0fs] A W up" % (time.time() - t0))
    time.sleep(3)
    if wb:
        send_key(wb, 0x57, True)
        print("[%.0fs] B W down" % (time.time() - t0))
    time.sleep(10)
    if wb:
        send_key(wb, 0x57, False)
        print("[%.0fs] B W up" % (time.time() - t0))
    time.sleep(15)
    srv.terminate()
    for p in (a, b):
        try:
            p.terminate()
        except Exception:
            pass
    time.sleep(2)
    kill(["reborn_client_m2-a.exe", "reborn_client_m2-b.exe", "Reborn.Server.exe"])

    for tag in ("reborn_client_m2-a.exe", "reborn_client_m2-b.exe"):
        log = newest_log(tag)
        print("==== %s -> %s" % (tag, log.name if log else "NO LOG"))
        if not log:
            continue
        for line in log.read_text(encoding="utf-8", errors="ignore").splitlines():
            if "net" in line and ("connect" in line or "join" in line or "entity" in line or "actor" in line or "correction" in line):
                print("   " + line)
    return 0


if __name__ == "__main__":
    sys.exit(main())
