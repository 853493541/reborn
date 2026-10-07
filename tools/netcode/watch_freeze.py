"""V2: freeze watchdog for the raw client - detect, grab logs, kill.

The raw client hangs/freezes regularly during V2 testing (stalled packet loop, scene
validation, etc.). This tool detects a freeze with three signals:
  1. the KGWin32App window does not answer WM_NULL within 1.5 s (SendMessageTimeout);
  2. IsHungAppWindow() reports it hung (this is what Windows shows as the ghost window);
  3. the process makes almost no CPU progress over --check-secs (default 3 s).
A window that is busy loading can briefly fail (1); (2)+(3) together mean frozen.

On a freeze it grabs evidence into a timestamped dir and then kills the client:
  <out>/freeze_<stamp>/
    freeze_report.txt      - signals, pid, window, cpu, timeline
    KG3D_<stamp>.log       - the client's own engine log (newest under logs/KG3D_Engine)
    DumpReport_<stamp>.log - if present
    stub_<stamp>.log       - newest C:\\jx3tmp\\game_stub*.log tail
    gw_<stamp>.log         - gateway log tail
    state_<stamp>.txt      - one-shot bind-state read (player/scene)
    screen_<stamp>.png     - screen capture (numeric fingerprint via image_stats.py)

Usage:
  python watch_freeze.py [--once] [--kill] [--check-secs 3] [--interval 5]
                         [--out C:\\jx3tmp] [--pid N]
Default: --kill on freeze (the user's rule: kill after grabbing logs).
"""
from __future__ import annotations

import argparse
import ctypes
import ctypes.wintypes as w
import os
import shutil
import struct
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import watch_world_bind as W

u32 = ctypes.windll.user32
k32 = ctypes.windll.kernel32
SMTO_ABORTIFHUNG = 0x0002
SMTO_BLOCK = 0x0001
CLIENT_LOGS = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\logs"


def find_game_window(pid):
    wins = []

    @ctypes.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
    def cb(hwnd, lparam):
        cls = ctypes.create_unicode_buffer(256)
        u32.GetClassNameW(hwnd, cls, 256)
        if cls.value == "KGWin32App" and u32.IsWindowVisible(hwnd):
            wpid = w.DWORD()
            u32.GetWindowThreadProcessId(hwnd, ctypes.byref(wpid))
            if pid is None or wpid.value == pid:
                wins.append(hwnd)
        return True

    u32.EnumWindows(cb, 0)
    return wins


def cpu_seconds(pid, secs):
    hp = k32.OpenProcess(0x400, False, pid)
    if not hp:
        return 0.0
    a = ctypes.c_ulonglong(); b = ctypes.c_ulonglong()
    c = ctypes.c_ulonglong(); d = ctypes.c_ulonglong()
    k32.GetProcessTimes(hp, ctypes.byref(a), ctypes.byref(b), ctypes.byref(c), ctypes.byref(d))
    t0 = c.value + d.value
    time.sleep(secs)
    k32.GetProcessTimes(hp, ctypes.byref(a), ctypes.byref(b), ctypes.byref(c), ctypes.byref(d))
    t1 = c.value + d.value
    k32.CloseHandle(hp)
    return (t1 - t0) / 1e7


def screen_hash():
    """Fast screen fingerprint - a loading screen animates, a frozen client is static."""
    try:
        from PIL import ImageGrab
        im = ImageGrab.grab().convert("L").resize((64, 36))
        return hash(im.tobytes())
    except Exception:
        return None


def check(pid, hwnd, check_secs):
    res = ctypes.c_size_t()
    r = u32.SendMessageTimeoutW(hwnd, 0, 0, 0, SMTO_ABORTIFHUNG | SMTO_BLOCK, 1500, ctypes.byref(res))
    no_pump = not bool(r)
    hung_flag = bool(u32.IsHungAppWindow(hwnd))
    cpu = cpu_seconds(pid, check_secs)
    return {"no_pump": no_pump, "hung_flag": hung_flag, "cpu": cpu, "frozen": False}


def newest_file(root, suffix):
    best = None
    for dp, dn, fn in os.walk(root):
        for f in fn:
            if f.endswith(suffix):
                p = os.path.join(dp, f)
                m = os.path.getmtime(p)
                if best is None or m > best[0]:
                    best = (m, p)
    return best[1] if best else None


def tail(path, lines=200):
    try:
        data = open(path, "rb").read()
        text = data.decode("utf-8", "replace").splitlines()
        return "\n".join(text[-lines:])
    except OSError:
        return None


def grab(pid, hwnd, info, out_root):
    stamp = time.strftime("%Y%m%d_%H%M%S")
    d = os.path.join(out_root, "freeze_%s" % stamp)
    os.makedirs(d, exist_ok=True)
    report = ["freeze detected at %s" % time.strftime("%Y-%m-%d %H:%M:%S"),
              "pid=%s hwnd=%s" % (pid, hwnd),
              "no_pump=%s hung_flag=%s cpu_secs=%.2f" % (info["no_pump"], info["hung_flag"], info["cpu"])]
    # client engine logs
    kg = newest_file(os.path.join(CLIENT_LOGS, "KG3D_Engine"), ".log")
    if kg:
        shutil.copy2(kg, os.path.join(d, "KG3D_%s.log" % stamp))
        report.append("KG3D log: %s" % kg)
    dr = newest_file(os.path.join(CLIENT_LOGS, "DumpReport"), ".log")
    if dr:
        shutil.copy2(dr, os.path.join(d, "DumpReport_%s.log" % stamp))
        report.append("DumpReport log: %s" % dr)
    # stub + gateway tails
    stub = newest_file(r"C:\jx3tmp", ".log")
    if stub and "game_stub" in os.path.basename(stub):
        t = tail(stub)
        if t:
            open(os.path.join(d, "stub_%s.log" % stamp), "w", encoding="utf-8").write(t)
            report.append("stub log: %s" % stub)
    gw = tail(r"C:\jx3tmp\gw_stdout.txt", 100)
    if gw:
        open(os.path.join(d, "gw_%s.log" % stamp), "w", encoding="utf-8").write(gw)
    # state read
    try:
        hp = k32.OpenProcess(0x438, False, pid)
        r = W.Reader(hp)
        base = W.module_base(pid, "jx3client")
        info2 = W.snapshot(r, base)
        open(os.path.join(d, "state_%s.txt" % stamp), "w", encoding="utf-8").write(W.render(info2))
        report.append("state: %s" % W.render(info2).replace("\n", " | "))
    except Exception as e:
        report.append("state read failed: %s" % e)
    # screenshot (numeric fingerprint later via tools/proof/image_stats.py)
    try:
        from PIL import ImageGrab
        ImageGrab.grab().save(os.path.join(d, "screen_%s.png" % stamp))
        report.append("screen captured")
    except Exception as e:
        report.append("screen capture failed: %s" % e)
    open(os.path.join(d, "freeze_report.txt"), "w", encoding="utf-8").write("\n".join(report) + "\n")
    return d, report


def kill(pid):
    hp = k32.OpenProcess(0x0001, False, pid)  # PROCESS_TERMINATE
    if hp:
        k32.TerminateProcess(hp, 1)
        k32.CloseHandle(hp)
        return True
    return False


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--once", action="store_true")
    ap.add_argument("--kill", action="store_true", default=True)
    ap.add_argument("--no-kill", dest="kill", action="store_false")
    ap.add_argument("--check-secs", type=float, default=3.0)
    ap.add_argument("--interval", type=float, default=5.0)
    ap.add_argument("--frozen-secs", type=float, default=90.0,
                    help="hung AND static screen for this long -> frozen (0 = immediate on hang)")
    ap.add_argument("--out", default=r"C:\jx3tmp")
    ap.add_argument("--pid", type=int, default=None)
    args = ap.parse_args()

    stable_since = None
    last_hash = None
    while True:
        pid = args.pid or W.find_client_pid()
        if not pid:
            print("[%s] no client" % time.strftime("%H:%M:%S"), flush=True)
            if args.once:
                return 1
            time.sleep(args.interval)
            continue
        wins = find_game_window(pid)
        if not wins:
            print("[%s] pid=%d has no visible KGWin32App window" % (time.strftime("%H:%M:%S"), pid), flush=True)
            if args.once:
                return 1
            time.sleep(args.interval)
            continue
        info = check(pid, wins[0], args.check_secs)
        h = screen_hash()
        now = time.time()
        if info["no_pump"] and info["hung_flag"]:
            if h is not None and h == last_hash:
                if stable_since is None:
                    stable_since = now
            else:
                stable_since = now if h is not None else None
        else:
            stable_since = None
        last_hash = h
        frozen = False
        if info["no_pump"] and info["hung_flag"]:
            if args.frozen_secs <= 0:
                frozen = True
            elif stable_since is not None and (now - stable_since) >= args.frozen_secs:
                frozen = True
        info["frozen"] = frozen
        tag = "FROZEN" if frozen else ("hung" if info["no_pump"] and info["hung_flag"] else "ok")
        print("[%s] pid=%d no_pump=%s hung=%s cpu=%.2f stable=%.0fs -> %s" % (
            time.strftime("%H:%M:%S"), pid, info["no_pump"], info["hung_flag"], info["cpu"],
            (now - stable_since) if stable_since else 0.0, tag), flush=True)
        if frozen:
            d, report = grab(pid, wins[0], info, args.out)
            print("\n".join(report), flush=True)
            print("evidence dir: %s" % d, flush=True)
            if args.kill:
                ok = kill(pid)
                print("client %s" % ("killed" if ok else "kill FAILED"), flush=True)
            return 0
        if args.once:
            return 0
        time.sleep(args.interval)


if __name__ == "__main__":
    sys.exit(main())
