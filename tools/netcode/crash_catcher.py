"""V2: crash catcher - copy new client crash artifacts before DumpReport deletes them.

DumpReport64.exe writes bin64\\minidump\\<guid>.xml (+ .dmp) on a client crash and deletes
them after the upload attempt. This loop copies any new *.xml / *.dmp / ExceptionNotCapture*
files into C:\\jx3tmp\\crashes\\ within ~100 ms.

Usage: python crash_catcher.py [--secs 3600] [--out C:\\jx3tmp\\crashes]
"""
from __future__ import annotations

import argparse
import os
import shutil
import time

SRC = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\minidump"


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--secs", type=float, default=3600.0)
    ap.add_argument("--out", default=r"C:\jx3tmp\crashes")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    seen = set()
    t0 = time.time()
    print("crash catcher watching %s -> %s" % (SRC, args.out), flush=True)
    while time.time() - t0 < args.secs:
        try:
            names = os.listdir(SRC)
        except OSError:
            names = []
        for n in names:
            if n in seen:
                continue
            low = n.lower()
            if not (low.endswith(".xml") or low.endswith(".dmp") or low.startswith("exceptionnotcapture")):
                continue
            src = os.path.join(SRC, n)
            if not os.path.isfile(src):
                continue
            seen.add(n)
            dst = os.path.join(args.out, "%d_%s" % (int(time.time()), n))
            try:
                shutil.copy2(src, dst)
                print("[%s] CAUGHT %s -> %s (%d bytes)" % (
                    time.strftime("%H:%M:%S"), n, dst, os.path.getsize(dst)), flush=True)
            except OSError as e:
                print("[%s] copy failed %s: %s" % (time.strftime("%H:%M:%S"), n, e), flush=True)
        time.sleep(0.1)
    print("crash catcher done", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
