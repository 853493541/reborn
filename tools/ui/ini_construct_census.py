#!/usr/bin/env python3
"""KGUI construct census / conformance check for the ui-process-app viewer.

Scans every shipped window INI under `ui-process-app/assets/ui/Config/Default`
and reports how often each engine construct appears, against the set the WPF
viewer implements. The target is zero "unhandled" usage; `--strict` exits 1
while any remains (the conformance gate of docs/ui/UI_RUNTIME_REPLAY.md).

Usage:
  python tools/ui/ini_construct_census.py [--dir <ini dir>] [--strict]
"""

import argparse
import os
import re
import sys
from collections import Counter

# Viewer coverage (keep in sync with ui-process-app/Engine/UiLayout.cs +
# LayoutPlan.cs). Anything outside these sets is reported as unhandled.
CONTAINER_TYPES = {
    "WndFrame", "WndWindow", "Handle", "Box", "Null", "WndContainer",
    "WndFlexContainer", "WndScroll", "WndNewScrollBar", "WndEdit", "WndMinimap",
}
# Types that are leaves or intentionally skipped; not layout containers.
LEAF_TYPES = {
    "Image", "Text", "WndButton", "WndCheckBox", "SFX", "WndSFX", "Animate",
    "Shadow", "WndScene", "Scene", "WndMovie", "WndWebCef",
    "WndPage", "WndPageSet", "WndList", "WndListNode", "WndTreeList",
    "WndTreeNode", "TreeLeaf", "FlexHandle",
}
# Page-sets are exact now: each WndPageSet shows one page (Page_i/CheckBox_i map +
# the CheckedWhenCreate=1 default, nested sets show their own default). Scene/movie/
# web are native-only (the static viewer cannot render them).
APPROXIMATE_TYPES = {
    "WndScene", "Scene", "WndMovie", "WndWebCef",
}
# PosType 3/4/5/9/12 are flow-relative (engine fn 0x180108600, jump table
# 0x180108998): each item is placed relative to the previous item's rect when the
# parent lays out items (FirstItemPosType != 0); outside a flow handle they keep
# the authored position. 0/1/2/6/7/8/10/11 are the absolute cases.
POS_TYPES = {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12}
# Engine dispatch (client KGUIX64.dll 0x180117D7C) dices 10/11/12/17/18/19;
# every other ImageType is a plain draw in the engine.
DICED_IMAGE_TYPES = {10, 11, 12, 17, 18, 19}
IMAGE_TYPES_PLAIN_ENGINE = {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 13, 14, 15, 16, 20}
# HandleType 1/2/3/5/6 flow as lists (engine dispatcher 0x1801064f5 + post pass
# 0x180106cc0); type 4 is the AppendString row (LayoutPlanBuilder.Build).
HANDLE_TYPES = {0, 1, 2, 3, 4, 5, 6}
# FirstItemPosType 0-12 decoded from the engine jump table 0x180108964 (first-item
# alignment sets the flow origin; the viewer implements all 13 cases).
FIRST_ITEM_POS_TYPES = set(range(0, 13))
ANCHOR_DST_SPECIAL = {"root", "client"}


def scan(ini_dir):
    files = sorted(f for f in os.listdir(ini_dir) if f.lower().endswith(".ini"))
    stats = {
        "files": len(files),
        "wndtype": Counter(),
        "wndtype_files": {},
        "postype": Counter(),
        "imagetype": Counter(),
        "handletype": Counter(),
        "firstitem": Counter(),
        "anchordst": Counter(),
    }
    for fn in files:
        text = open(os.path.join(ini_dir, fn), encoding="gb18030", errors="replace").read()
        for m in re.finditer(r"^\._WndType=(\S+)\s*$", text, re.M):
            t = m.group(1)
            stats["wndtype"][t] += 1
            stats["wndtype_files"].setdefault(t, set()).add(fn)
        for m in re.finditer(r"^PosType=(\d+)\s*$", text, re.M):
            stats["postype"][int(m.group(1))] += 1
        for m in re.finditer(r"^ImageType=(\d+)\s*$", text, re.M):
            stats["imagetype"][int(m.group(1))] += 1
        for m in re.finditer(r"^HandleType=(\d+)\s*$", text, re.M):
            stats["handletype"][int(m.group(1))] += 1
        for m in re.finditer(r"^FirstItemPosType=(\d+)\s*$", text, re.M):
            stats["firstitem"][int(m.group(1))] += 1
        for m in re.finditer(r"^AnchorDst=(\S+)\s*$", text, re.M):
            stats["anchordst"][m.group(1)] += 1
    return stats


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default=None,
                    help="INI dir (default: <repo>/ui-process-app/assets/ui/Config/Default)")
    ap.add_argument("--strict", action="store_true",
                    help="exit 1 when any unhandled construct is used")
    args = ap.parse_args()

    ini_dir = args.dir
    if ini_dir is None:
        repo = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
        ini_dir = os.path.join(repo, "ui-process-app", "assets", "ui", "Config", "Default")
    if not os.path.isdir(ini_dir):
        print("INI dir not found: " + ini_dir)
        return 2

    s = scan(ini_dir)
    print("files=%d" % s["files"])
    unhandled = []

    print("\n== WndType ==")
    for t, c in s["wndtype"].most_common():
        nfiles = len(s["wndtype_files"].get(t, ()))
        if t in CONTAINER_TYPES or t in LEAF_TYPES:
            note = "approx" if t in APPROXIMATE_TYPES else "ok"
        else:
            note = "UNHANDLED"
            unhandled.append(("WndType", t, c, nfiles))
        print("  %-20s %6d  files=%-4d %s" % (t, c, nfiles, note))

    def report(name, counter, handled):
        print("\n== %s ==" % name)
        for k, c in sorted(counter.items()):
            note = "ok" if k in handled else "UNHANDLED"
            if note == "UNHANDLED":
                unhandled.append((name, k, c, 0))
            print("  %-6s %6d  %s" % (k, c, note))

    report("PosType", s["postype"], POS_TYPES)
    report("ImageType", s["imagetype"], DICED_IMAGE_TYPES | IMAGE_TYPES_PLAIN_ENGINE)
    report("HandleType", s["handletype"], HANDLE_TYPES)
    report("FirstItemPosType", s["firstitem"], FIRST_ITEM_POS_TYPES)

    print("\n== AnchorDst ==")
    special = sum(c for k, c in s["anchordst"].items() if k.lower() in ANCHOR_DST_SPECIAL)
    rel = sum(c for k, c in s["anchordst"].items() if k.lower() not in ANCHOR_DST_SPECIAL)
    print("  special(root/client) %6d  relative/name %6d  ok" % (special, rel))

    print("\nunhandled constructs: %d" % len(unhandled))
    for name, k, c, nf in unhandled:
        print("  %-18s %-12s count=%-6d files=%d" % (name, k, c, nf))
    if args.strict and unhandled:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
