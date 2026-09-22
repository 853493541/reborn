#!/usr/bin/env python3
"""Extract gameplay scaling fields from extracted JX3 skill scripts.

Reads the plaintext skill Lua scripts (compiled ones are skipped) and reports:
  - tSkillData per-level entries (damage base/rand, speed, cost)
  - skill.nWeaponDamagePercent (weapon damage scaling, 1024 = 100%)
  - AddAttribute(ATTRIBUTE_TYPE.X, v1, v2) calls, with type name
  - key timing/range fields (frames, radii, cooldowns, dash)

Usage:
  python tools/netcode/scan_skill_scripts.py --root "<extracted scripts/skill>" \
      --out proof/netcode/skill_scaling_report.tsv [--skill 太阴指]
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

FIELDS = [
    "nWeaponDamagePercent", "nDamageBase", "nDamageRand", "nSpeed",
    "nDashFrame", "nMaxRadius", "nMinRadius", "nAreaRadius", "nAngleRange",
    "nPrepareFrames", "nChannelFrame", "nTargetCountLimit", "nCostMana",
    "nBaseThreat",
]
ATTR_RE = re.compile(r"ATTRIBUTE_TYPE\.([A-Z0-9_]+)")
NUM_RE = re.compile(r"(-?\d+(?:\.\d+)?)")
ASSIGN_RE = re.compile(r"(n[A-Za-z0-9_]+)\s*=\s*([^;]+);")


def read_text(path: Path) -> str | None:
    b = path.read_bytes()
    if b[:4] == b"\x1bLua":
        return None
    try:
        t = b.decode("gb18030")
    except UnicodeDecodeError:
        return None
    if "tSkillData" in t or "AddAttribute" in t or "function " in t:
        return t
    return None


def parse_script(text: str) -> dict:
    out: dict = {"attrs": [], "fields": {}, "levels": []}

    for m in ATTR_RE.finditer(text):
        tail = text[m.end(): m.end() + 200]
        nums = NUM_RE.findall(tail)
        out["attrs"].append((m.group(1), nums[:2]))

    for m in ASSIGN_RE.finditer(text):
        name, value = m.group(1), m.group(2).strip()
        if name in FIELDS and name not in out["fields"]:
            out["fields"][name] = value

    block = re.search(r"tSkillData\s*=\s*\{(.*?)\n\};", text, re.S)
    if block:
        for i, line in enumerate(block.group(1).splitlines()):
            if "{" not in line:
                continue
            entry = {}
            for m in re.finditer(r"([A-Za-z0-9_]+)\s*=\s*([^,}]+)", line):
                entry[m.group(1)] = m.group(2).strip()
            if entry:
                out["levels"].append((i + 1, entry))
    return out


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--root", type=Path, required=True)
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--skill", default="", help="filter by name substring")
    args = ap.parse_args(argv)

    rows = []
    scanned = skipped = 0
    for path in sorted(args.root.rglob("*.lua")):
        text = read_text(path)
        if text is None:
            skipped += 1
            continue
        if args.skill and args.skill not in path.name:
            continue
        scanned += 1
        info = parse_script(text)
        if not info["attrs"] and not info["fields"] and not info["levels"]:
            continue
        rel = path.relative_to(args.root)
        attrs = "; ".join(f"{n}({','.join(v)})" for n, v in info["attrs"])
        fields = "; ".join(f"{k}={v}" for k, v in info["fields"].items())
        levels = "; ".join(
            f"L{lvl}:{','.join(f'{k}={v}' for k, v in entry.items())}"
            for lvl, entry in info["levels"][:3]
        )
        rows.append(f"{rel}\t{fields}\t{attrs}\t{levels}")

    header = "script\tfields\tattributes\tlevels(1-3)"
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(header + "\n" + "\n".join(rows) + "\n", encoding="utf-8")
    print(f"{scanned} plaintext scripts scanned ({skipped} compiled/other), {len(rows)} rows -> {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
