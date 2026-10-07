"""V2: incident loop - watch the raw client; on FREEZE or EXIT collect the reason, kill, report.

The user's rule: as soon as the client freezes/crashes -> log the reason -> kill it -> fix ->
retry. This tool is the "log + reason" half: it watches the client and, on an incident,
writes C:\\jx3tmp\\incidents\\<stamp>\\reason.txt containing:
  - the exit code (parsed from the newest emul_*.txt "client EXITED code=0x..."),
  - the newest DumpReport crash summary (CrashType/DumpKey/Module lines),
  - the client's own KG3D engine log TAIL (the errors right before death),
  - any new crash XML/dmp caught in C:\\jx3tmp\\crashes (exception/module/address),
  - the stub log tail, a one-shot state read, a screenshot.
It then kills the client if it is still alive and exits 0 (an incident happened).

Usage: python incident_report.py [--frozen-secs 90] [--interval 2] [--no-kill]
"""
from __future__ import annotations

import argparse
import ctypes
import ctypes.wintypes as w
import os
import shutil
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import watch_world_bind as W
import watch_freeze as F

u32 = ctypes.windll.user32
k32 = ctypes.windll.kernel32
LOGS = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\logs"
MINIDUMP = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\minidump"


def newest(glob_root, suffix, since=None):
    best = None
    for dp, dn, fn in os.walk(glob_root):
        for f in fn:
            if f.endswith(suffix):
                p = os.path.join(dp, f)
                try:
                    m = os.path.getmtime(p)
                except OSError:
                    continue
                if since is not None and m < since:
                    continue
                if best is None or m > best[0]:
                    best = (m, p)
    return best[1] if best else None


def tail_text(path, lines=60):
    try:
        data = open(path, "rb").read().decode("utf-8", "replace").splitlines()
        return "\n".join(data[-lines:])
    except OSError:
        return None


def exit_code():
    p = newest(r"C:\jx3tmp", ".txt")
    best = None
    for f in os.listdir(r"C:\jx3tmp"):
        if f.startswith("emul_") and f.endswith(".txt"):
            fp = os.path.join(r"C:\jx3tmp", f)
            try:
                m = os.path.getmtime(fp)
            except OSError:
                continue
            if best is None or m > best[0]:
                best = (m, fp)
    if not best:
        return None, None
    t = tail_text(best[1], 40)
    code = None
    for line in (t or "").splitlines():
        if "client EXITED" in line:
            code = line.strip()
    return best[1], code


def dump_report_summary():
    p = newest(os.path.join(LOGS, "DumpReport"), ".log")
    if not p:
        return None, None
    lines = []
    for line in (tail_text(p, 400) or "").splitlines():
        if "CrashType" in line or "DumpKey" in line or "No solution" in line or "Project:" in line:
            lines.append(line.strip())
    return p, "\n".join(lines[:8])


def crash_xml_summary():
    root = r"C:\jx3tmp\crashes"
    if not os.path.isdir(root):
        return None, None
    best = None
    for f in os.listdir(root):
        if f.endswith(".xml"):
            p = os.path.join(root, f)
            m = os.path.getmtime(p)
            if best is None or m > best[0]:
                best = (m, p)
    if not best:
        return None, None
    txt = tail_text(best[1], 200) or ""
    keep = []
    for line in txt.splitlines():
        s = line.strip()
        if s.startswith("<ExceptionCode>") or s.startswith("<ExceptionModuleName>") or s.startswith("<ExceptionAddress>") or s.startswith("<ProcessName>") or s.startswith("<ExceptionDescription>"):
            keep.append(s)
    return best[1], "\n".join(keep) if keep else "(no exception fields - empty startup report)"


def collect(pid, hwnd, frozen_info, out_root):
    stamp = time.strftime("%Y%m%d_%H%M%S")
    d = os.path.join(out_root, "incident_%s" % stamp)
    os.makedirs(d, exist_ok=True)
    r = ["V2 client incident %s" % time.strftime("%Y-%m-%d %H:%M:%S"),
         "pid=%s hwnd=%s" % (pid, hwnd),
         "frozen=%s no_pump=%s hung=%s cpu=%.2f" % (
             frozen_info.get("frozen"), frozen_info.get("no_pump"),
             frozen_info.get("hung_flag"), frozen_info.get("cpu", -1)),
         ""]
    emul, code = exit_code()
    r.append("exit: %s (%s)" % (code or "client still alive (freeze incident)", emul))
    r.append("")
    dr, summary = dump_report_summary()
    r.append("DumpReport: %s" % dr)
    r.append(summary or "(no summary)")
    r.append("")
    xml, xsum = crash_xml_summary()
    r.append("crash xml: %s" % xml)
    r.append(xsum or "(none)")
    r.append("")
    kg = newest(os.path.join(LOGS, "KG3D_Engine"), ".log")
    r.append("KG3D log: %s" % kg)
    if kg:
        shutil.copy2(kg, os.path.join(d, "KG3D.log"))
        r.append("--- KG3D tail (last errors before death) ---")
        r.append(tail_text(kg, 40) or "")
    stub = F.newest_stub_log()
    if stub:
        r.append("")
        r.append("stub: %s" % stub)
        r.append("--- stub tail ---")
        r.append(tail_text(stub, 25) or "")
    # state + screen
    if pid and F.find_game_window(pid):
        try:
            hp = k32.OpenProcess(0x438, False, pid)
            base = W.module_base(pid, "jx3client")
            info = W.snapshot(W.Reader(hp), base)
            r.append("")
            r.append("state: %s" % W.render(info).replace("\n", " | "))
        except Exception as e:
            r.append("state read failed: %s" % e)
        try:
            from PIL import ImageGrab
            ImageGrab.grab().save(os.path.join(d, "screen.png"))
        except Exception:
            pass
    report = "\n".join(r)
    open(os.path.join(d, "reason.txt"), "w", encoding="utf-8").write(report + "\n")
    return d, report


def safe_print(line):
    """The report can contain GB18030 map names / U+FFFD; the console is GBK.
    Never let a print encoding error kill the watcher before it reports."""
    try:
        print(line, flush=True)
    except UnicodeEncodeError:
        print(line.encode("gbk", "replace").decode("gbk", "replace"), flush=True)


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--frozen-secs", type=float, default=90.0)
    ap.add_argument("--interval", type=float, default=2.0)
    ap.add_argument("--out", default=r"C:\jx3tmp\incidents")
    ap.add_argument("--no-kill", action="store_true")
    args = ap.parse_args()

    pid = W.find_client_pid()
    if not pid:
        print("no client to watch")
        return 1
    print("watching pid=%d (incident on freeze or exit)" % pid, flush=True)
    stable_since = None
    last_hash = None
    while True:
        alive = W.find_client_pid()
        if not alive or alive != pid:
            info = {"frozen": "exit", "no_pump": False, "hung_flag": False, "cpu": -1.0}
            d, rep = collect(pid, 0, info, args.out)
            safe_print(rep)
            print("incident dir: %s" % d, flush=True)
            return 0
        wins = F.find_game_window(pid)
        if wins:
            info = F.check(pid, wins[0], 3.0)
            h = F.screen_hash()
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
            frozen = info["no_pump"] and info["hung_flag"] and stable_since is not None and (now - stable_since) >= args.frozen_secs
            info["frozen"] = frozen
            if frozen:
                d, rep = collect(pid, wins[0], info, args.out)
                safe_print(rep)
                if not args.no_kill:
                    F.kill(pid)
                    print("client killed", flush=True)
                print("incident dir: %s" % d, flush=True)
                return 0
        time.sleep(args.interval)


if __name__ == "__main__":
    raise SystemExit(main())
