#!/usr/bin/env python3
"""Replay every window script's OnFrameCreate against its own INI and record the
runtime mutations (docs/ui/UI_RUNTIME_REPLAY.md).

The scripts are standard Lua 5.1 bytecode and need a 32-bit PUC Lua 5.1
interpreter (see the doc for the build). Point `LUA32` at it, or use the default
temp build path from the 2026-10-04 session.

Usage:
  python tools/ui/replay_all.py [--lua32 <exe>] [--out-dir <dir>] [--only <stem>]
"""

import argparse
import os
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_INI_DIR = os.path.join(REPO, "ui-process-app", "assets", "ui", "Config", "Default")
DEFAULT_OUT = os.path.join(REPO, "ui-process-app", "Data", "runtime_state")
DEFAULT_LUA32 = r"C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\lua-5.1.5\lua-5.1.5\build32\lua32.exe"
HARNESS = os.path.join(REPO, "tools", "ui", "replay_harness.lua")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--lua32", default=os.environ.get("LUA32", DEFAULT_LUA32))
    ap.add_argument("--ini-dir", default=DEFAULT_INI_DIR)
    ap.add_argument("--out-dir", default=DEFAULT_OUT)
    ap.add_argument("--only", default=None, help="replay a single stem")
    args = ap.parse_args()

    if not os.path.exists(args.lua32):
        print("lua32 not found: " + args.lua32)
        return 2
    os.makedirs(args.out_dir, exist_ok=True)

    stems = []
    for fn in sorted(os.listdir(args.ini_dir)):
        if not fn.lower().endswith(".ini"):
            continue
        stem = fn[:-4]
        if not os.path.exists(os.path.join(args.ini_dir, stem + ".lua")):
            continue
        if args.only and stem.lower() != args.only.lower():
            continue
        stems.append(stem)

    print("scripted windows: %d" % len(stems))
    summary = []
    ok_count = err_count = 0
    for stem in stems:
        ini = os.path.join(args.ini_dir, stem + ".ini")
        lua = os.path.join(args.ini_dir, stem + ".lua")
        out = os.path.join(args.out_dir, stem + ".tsv")
        r = subprocess.run([args.lua32, HARNESS, lua, "auto", ini, out],
                           capture_output=True, text=True, timeout=120)
        result = None
        for line in (r.stdout or "").splitlines():
            if line.startswith("RESULT "):
                result = line[len("RESULT "):]
        if result is None:
            result = "ERR no-result"
        if result.startswith("OK"):
            ok_count += 1
        else:
            err_count += 1
        mutations = 0
        for tok in result.split():
            if tok.startswith("mutations="):
                mutations = int(tok.split("=")[1])
        summary.append((stem, result, mutations))
        print("%-40s %s" % (stem, result[:110]))

    summary_path = os.path.join(args.out_dir, "replay_summary.tsv")
    with open(summary_path, "w", encoding="utf-8", newline="") as f:
        f.write("window\tresult\tmutations\n")
        for stem, result, mutations in summary:
            f.write("%s\t%s\t%d\n" % (stem, result, mutations))
    print("\nOK=%d ERR=%d -> %s" % (ok_count, err_count, summary_path))
    return 0


if __name__ == "__main__":
    sys.exit(main())
