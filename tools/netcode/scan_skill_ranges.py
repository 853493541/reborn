#!/usr/bin/env python3
"""Extract cast/effect ranges from JX3 skill scripts (plaintext).

Fields (see scripts/skill/Default.lua for the documented API):
  nMinRadius / nMaxRadius   cast distance window   (X * LENGTH_BASE = X 尺)
  nAreaRadius               effect radius          (尺)
  nAngleRange               cone/sector angle      (1/256 turn; 256 = 360°)
  nRectWidth / nHeight      rectangle AOE size     (尺)
  nProtectRadius            safe ring radius       (尺)
  nTargetCountLimit         max targets

Output TSV: path, school, then the range fields in 尺 / degrees.

Usage:
  python tools/netcode/scan_skill_ranges.py --root "<...>/scripts/skill" \
      --out proof/netcode/skill_range_report.tsv [--name 太阴指]
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

FIELDS = [
    "nMinRadius", "nMaxRadius", "nAreaRadius", "nAngleRange",
    "nRectWidth", "nHeight", "nProtectRadius", "nTargetCountLimit",
    "nChannelFrame", "nPrepareFrames",
]
ASSIGN_RE = re.compile(r"\b(" + "|".join(FIELDS) + r")\s*=\s*([^;\n]+);?")


def to_chi(expr: str) -> str:
    e = expr.strip()
    e = re.sub(r"--.*$", "", e).strip()
    m = re.match(r"^(-?[\d\.]+)\s*\*\s*LENGTH_BASE$", e)
    if m:
        return f"{float(m.group(1)):g}"
    m = re.match(r"^(-?[\d\.]+)\s*\*\s*64$", e)
    if m:
        return f"{float(m.group(1)):g}"
    m = re.match(r"^(-?[\d\.]+)\s*\*\s*(\d+)\s*\*\s*64$", e)
    if m:
        return f"{float(m.group(1)) * float(m.group(2)):g}"
    if re.match(r"^-?[\d\.]+$", e):
        return f"{float(e):g}(raw)"
    return e


def angle_deg(expr: str) -> str:
    e = expr.strip()
    if re.match(r"^-?[\d\.]+$", e):
        deg = float(e) * 360.0 / 256.0
        return f"{deg:g}"
    return e


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--root", type=Path, required=True)
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--name", default="")
    args = ap.parse_args(argv)

    rows = []
    for path in sorted(args.root.rglob("*.lua")):
        if args.name and args.name not in path.name:
            continue
        b = path.read_bytes()
        if b[:4] == b"\x1bLua":
            continue
        try:
            text = b.decode("gb18030")
        except UnicodeDecodeError:
            continue
        if "nMaxRadius" not in text and "nAreaRadius" not in text:
            continue
        vals: dict[str, str] = {}
        for m in ASSIGN_RE.finditer(text):
            name, expr = m.group(1), m.group(2)
            if name in vals:
                continue
            if name == "nAngleRange":
                vals[name] = angle_deg(expr)
            elif name in ("nPrepareFrames", "nChannelFrame"):
                vals[name] = re.sub(r"--.*$", "", expr).strip()[:20]
            else:
                vals[name] = to_chi(expr)
        rel = path.relative_to(args.root)
        school = rel.parts[0] if len(rel.parts) > 1 else ""
        rows.append((
            str(rel).replace("\t", " "),
            school,
            vals.get("nMinRadius", ""), vals.get("nMaxRadius", ""),
            vals.get("nAreaRadius", ""), vals.get("nAngleRange", ""),
            vals.get("nRectWidth", ""), vals.get("nProtectRadius", ""),
            vals.get("nTargetCountLimit", ""),
        ))

    header = "script\tschool\tmin_range_chi\tmax_range_chi\tarea_radius_chi\tangle_deg\trect_width_chi\tprotect_radius_chi\ttargets"
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(
        header + "\n" + "\n".join("\t".join(r) for r in rows) + "\n",
        encoding="utf-8",
    )
    print(f"{len(rows)} scripts with ranges -> {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
