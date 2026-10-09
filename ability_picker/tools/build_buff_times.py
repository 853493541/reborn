#!/usr/bin/env python3
"""Extract Buff.tab time fields for the buffs the roster applies -> buff_times_f1.tsv.

Client truth (read-only, cited in docs/pvp/ABILITY_FOLLOWUP_PLAN.md): a buff's timer
is `Count (col 13) x Interval (col 14)` frames -- `KScriptFuncList::LuaGetBuffTime`
(JX3ClientX64.exe @0x1401c0730) returns exactly `(Count*Interval, Count, Interval)` --
and for CC buffs the control duration is `Intensity` (col 10) frames (proof/pvp/
buff_control_system.md). The frame base is 16 fps (cast frames confirm it:
nChannelFrame=64 -> 4 s, nPrepareFrames=24 -> 1.5 s). This tool only *extracts* the
raw fields; the CC-vs-non-CC interpretation is the client's, not the builder's.

Usage:
  python build_buff_times.py [--buff-tab <Buff.tab>] [--out <tsv>]
"""
from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
import tempfile

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

_REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DATA = os.path.join(_REPO, "ability_picker", "data")
PKV4 = r"C:\SeasunGame\Game\JX3\bin\zhcn_hd\bin64\PakV4SfxExtract.exe"

COLS = {"ID": 0, "FunctionType": 3, "Level": 9, "Intensity": 10, "Count": 13, "Interval": 14}


def extract_buff_tab(dest: str) -> str:
    """Extract settings/skill/Buff.tab via the official tool into dest (gbk pathlist)."""
    os.makedirs(dest, exist_ok=True)
    plist = os.path.join(dest, "_paths.txt")
    open(plist, "wb").write("settings/skill/Buff.tab\r\n".encode("gb18030", "replace"))
    subprocess.run([PKV4, plist, dest], cwd=os.path.dirname(PKV4),
                   capture_output=True, timeout=300)
    return os.path.join(dest, "settings", "skill", "Buff.tab")


def referenced_ids() -> set:
    """All buff ids the roster applies: apply_f1.tsv (addBuffs/delBuffs) + mechanics
    `CALL_BUFF` ops (the client's MechanicProgram treats a CALL_BUFF arg as a buff id)."""
    ids = set()
    path = os.path.join(DATA, "apply_f1.tsv")
    if os.path.exists(path):
        for ln in open(path, encoding="utf-8").read().splitlines()[1:]:
            p = ln.split("\t")
            if len(p) < 5:
                continue
            for cell in (p[3], p[4]):
                for tok in cell.split(";"):
                    if tok.strip().isdigit():
                        ids.add(tok.strip())
    mpath = os.path.join(DATA, "mechanics_f1.tsv")
    if os.path.exists(mpath):
        header = open(mpath, encoding="utf-8").read().splitlines()[0].split("\t")
        oi = header.index("ops") if "ops" in header else -1
        if oi >= 0:
            for ln in open(mpath, encoding="utf-8").read().splitlines()[1:]:
                p = ln.split("\t")
                if oi >= len(p):
                    continue
                for m in re.finditer(r"\|CALL_BUFF\|(\d+)", p[oi]):
                    ids.add(m.group(1))
    return ids


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--buff-tab", default="", help="existing Buff.tab (else extract from paks)")
    ap.add_argument("--out", default=os.path.join(DATA, "buff_times_f1.tsv"))
    args = ap.parse_args()

    tab = args.buff_tab
    tmp = None
    if not tab:
        tmp = tempfile.mkdtemp(prefix="buff_times_")
        tab = extract_buff_tab(tmp)
    if not os.path.exists(tab):
        print("Buff.tab not found: %s" % tab, file=sys.stderr)
        return 1

    want = referenced_ids()
    text = open(tab, "rb").read().decode("gb18030", "replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    header = rows[0]
    idx = {name: header.index(name) for name in COLS if name in header}
    missing = [n for n in COLS if n not in idx]
    if missing:
        print("Buff.tab header missing: %s" % missing, file=sys.stderr)
        return 1

    def cell(r, name):
        i = idx[name]
        return r[i].strip() if i < len(r) else ""

    out = []
    for r in rows[1:]:
        if not r or r[0].strip() not in want:
            continue
        out.append([r[0].strip(), cell(r, "Level"), cell(r, "FunctionType"),
                    cell(r, "Count"), cell(r, "Interval"), cell(r, "Intensity")])
    out.sort(key=lambda x: (int(x[0]), int(x[1] or 0)))

    with open(args.out, "w", encoding="utf-8", newline="\n") as f:
        f.write("buffId\tlevel\tfunctionType\tcount\tinterval\tintensity\n")
        for r in out:
            f.write("\t".join(r) + "\n")

    ids = sorted({r[0] for r in out}, key=int)
    print("buff_times: %d buffs / %d rows (of %d referenced ids) -> %s"
          % (len(ids), len(out), len(want), args.out))
    print("  sample:", out[:3])
    if tmp:
        print("  (Buff.tab extracted to %s)" % tab)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
