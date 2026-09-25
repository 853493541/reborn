#!/usr/bin/env python3
"""Aggregate cast/channel/resource/GCD evidence from skill_cast_fields.tsv.

Usage: python tools/pvp/analyze_cast.py [--tsv PATH]
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

NUM = re.compile(r"\s*(-?\d+(?:\.\d+)?)")


def firstnum(s: str | None) -> float | None:
    if not s:
        return None
    m = NUM.match(s)
    return float(m.group(1)) if m else None


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--tsv", type=Path, default=Path("proof/pvp/cast/skill_cast_fields.tsv"))
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    rows = [l.rstrip("\n").split("\t") for l in args.tsv.open(encoding="utf-8")][1:]

    print("== channel: nChannelFrame > 0 (true channels)")
    n = 0
    for r in rows:
        if r[2] != "plain":
            continue
        f = json.loads(r[3])
        cf = firstnum(f.get("nChannelFrame"))
        if cf and cf > 0:
            print(f"  {r[0]}\n      nChannelFrame={f.get('nChannelFrame')!r} "
                  f"nChannelInterval={f.get('nChannelInterval')!r} "
                  f"nMinChannelFrame={f.get('nMinChannelFrame')!r} "
                  f"bInstantChannel={f.get('bInstantChannel')!r}")
            n += 1
            if n >= 25:
                break

    print("\n== prepare: nPrepareFrames > 0 and numeric")
    n = 0
    for r in rows:
        if r[2] != "plain":
            continue
        f = json.loads(r[3])
        pf = firstnum(f.get("nPrepareFrames"))
        if pf and pf > 0:
            print(f"  {r[0]}\n      nPrepareFrames={f.get('nPrepareFrames')!r} "
                  f"nMinPrepareFrames={f.get('nMinPrepareFrames')!r}")
            n += 1
            if n >= 25:
                break

    print("\n== resource fields per school (counts of plaintext scripts)")
    per_school = defaultdict(Counter)
    for r in rows:
        if r[2] != "plain":
            continue
        f = json.loads(r[3])
        for k, v in f.items():
            if k.startswith(("nCost", "nNeed", "bIsAccumulate")) and v and v not in ("0", "false"):
                per_school[r[1]][k] += 1
    for school, cnt in sorted(per_school.items(), key=lambda x: -sum(x[1].values()))[:28]:
        top = ", ".join(f"{k}={v}" for k, v in cnt.most_common(8))
        print(f"  {school:<24} {top}")

    print("\n== GCD calls (plaintext)")
    gcd = Counter()
    for r in rows:
        if r[2] != "plain":
            continue
        for call in r[4].split(";"):
            if call:
                gcd[call] += 1
    for k, v in gcd.most_common(30):
        print(f"  {v:5d}  {k}")

    print("\n== bytecode scripts mentioning SetPublicCoolDown / OverDraft")
    for r in rows:
        if r[2] == "bytecode" and ("SetPublicCoolDown" in r[4] or "OverDraft" in r[4]):
            pass
    print("  (values for bytecode recovered via lua51_disasm tool)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
