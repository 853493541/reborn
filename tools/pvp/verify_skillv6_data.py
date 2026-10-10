#!/usr/bin/env python3
"""Verify the v6 ability datasets in ability_picker/data are present, well-formed and consistent.

Offline, deterministic, no engine/client. Guards the session's data work (FX4 hit_fx, buff
durations, AoE geometry). Exit 0 = all checks pass.

Usage: python tools/pvp/verify_skillv6_data.py
"""
from __future__ import annotations

import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DATA = os.path.join(ROOT, "ability_picker", "data")
fails = []


def check(ok, msg):
    print(("PASS: " if ok else "FAIL: ") + msg)
    if not ok:
        fails.append(msg)


def load(name):
    p = os.path.join(DATA, name)
    if not os.path.exists(p):
        return None
    with open(p, encoding="utf-8") as f:
        lines = f.read().splitlines()
    return lines[0].split("\t"), [l.split("\t") for l in lines[1:] if l.strip()]


def main():
    roster = load("roster_f1.tsv")
    check(roster is not None and len(roster[1]) == 154, "roster_f1.tsv has 154 abilities")
    roster_ids = set(r[0] for r in roster[1])
    cast_mode = {r[0]: (r[6] if len(r) > 6 else "") for r in roster[1]}

    mech = load("mechanics_f1.tsv")
    check(mech is not None, "mechanics_f1.tsv present")
    if mech:
        h = mech[0]
        mi, ma, ar = h.index("minRadius"), h.index("maxRadius"), h.index("areaRadius")
        # area skills must have a resolvable radius source: areaRadius>0 OR maxRadius>0. The client
        # falls back to maxRadius when areaRadius<=0 (fixed hit-all bug), and hits the current target
        # only when both are unset; the only both-unset skill (30235) is AV-blacklisted.
        bl_path = os.path.join(DATA, "av_blacklist_f1.txt")
        bl = set(l.strip() for l in open(bl_path, encoding="utf-8")) if os.path.exists(bl_path) else set()
        bad_area = [r[0] for r in mech[1]
                    if cast_mode.get(r[0]) in ("PointArea", "TargetArea", "CasterArea")
                    and float(r[ar] or -1) <= 0 and float(r[ma] or -1) <= 0 and r[0] not in bl]
        check(not bad_area, "mechanics: area skills have a radius source, else blacklisted (%s)"
              % bad_area)

    bt = load("buff_times_f1.tsv")
    check(bt is not None and len(bt[1]) >= 100, "buff_times_f1.tsv has >=100 buff rows")
    if bt:
        h = bt[0]
        ci, ii = h.index("count"), h.index("interval")
        ok = all((r[ci].isdigit() and r[ii].isdigit()) for r in bt[1] if len(r) > ii)
        check(ok, "buff_times: every row has integer count/interval")

    hf = load("hit_fx_f1.tsv")
    check(hf is not None and len(hf[1]) == 154, "hit_fx_f1.tsv has 154 abilities")
    if hf:
        h = hf[0]
        pb = h.index("isPlayBehit")
        n = sum(1 for r in hf[1] if len(r) > pb and r[pb] == "1")
        check(n > 0, "hit_fx: some abilities set isPlayBehit (%d)" % n)

    print("\n%d check(s) failed" % len(fails))
    return 1 if fails else 0


if __name__ == "__main__":
    raise SystemExit(main())
