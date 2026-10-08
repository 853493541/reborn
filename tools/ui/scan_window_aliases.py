#!/usr/bin/env python3
"""Scan the window scripts' own SETGLOBAL definitions for window-opener globals.

The engine resolves `OpenBankPanel()`-style helpers through globals that the window
modules define (`SETGLOBAL OpenBankPanel` in BigBankPanel.lua). A single-module
replay sees only the call site, not the definition, so the recorded popup chain
carries a bare helper name. This index maps each opener global to the module (ini
stem) that defines it, and the viewer uses it to follow the chain.

The window scripts are Lua 5.1 bytecode; the listing comes from luac32 -l -p (the
same 32-bit PUC Lua build as the replay harness).

Usage:
  python tools/ui/scan_window_aliases.py [--lua32 <exe>] [--out <tsv>]
"""

import argparse
import os
import re
import subprocess
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_DIR = os.path.join(REPO, "ui-process-app", "assets", "ui", "Config", "Default")
DEFAULT_OUT = os.path.join(REPO, "ui-process-app", "Data", "ui_window_aliases.tsv")
DEFAULT_LUA32 = r"C:\Users\ZHIBIN~1\AppData\Local\Temp\opencode\lua-5.1.5\lua-5.1.5\build32\luac32.exe"

SETGLOBAL = re.compile(r"SETGLOBAL\s+\d+\s+-\d+\s+;\s+(\S+)")


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--lua32", default=os.environ.get("LUA32", DEFAULT_LUA32))
    ap.add_argument("--dir", default=DEFAULT_DIR)
    ap.add_argument("--out", default=DEFAULT_OUT)
    args = ap.parse_args()

    if not os.path.exists(args.lua32):
        print("luac32 not found: " + args.lua32)
        return 2

    aliases = {}
    scripts = []
    for root, _dirs, files in os.walk(args.dir):
        for fn in sorted(files):
            if not fn.lower().endswith(".lua") or fn.lower().endswith(".decompiled.lua"):
                continue
            scripts.append(os.path.join(root, fn))

    for path in sorted(scripts):
        try:
            r = subprocess.run([args.lua32, "-l", "-p", path], capture_output=True, text=True, timeout=60)
        except subprocess.TimeoutExpired:
            continue
        if r.returncode != 0:
            continue
        stem = os.path.splitext(os.path.basename(path))[0]
        for line in r.stdout.splitlines():
            m = SETGLOBAL.search(line)
            if not m:
                continue
            name = m.group(1)
            if not name.startswith(("Open", "Close")):
                continue
            if name not in aliases:
                aliases[name] = stem

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        for name in sorted(aliases):
            f.write("%s\t%s\n" % (name, aliases[name]))
    print("aliases=%d scripts=%d -> %s" % (len(aliases), len(scripts), args.out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
