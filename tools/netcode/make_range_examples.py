#!/usr/bin/env python3
"""Build a curated ability-range example sheet from skill_range_report.tsv.

Adds named player skills plus one example per range bucket, and cross-checks
tooltip text (ui/Scheme/Case/Skill.txt) for player-visible 尺 wording.

Usage:
  python tools/netcode/make_range_examples.py --report proof/netcode/skill_data/skill_range_report.tsv \
      --out proof/netcode/skill_data/ability_range_examples.md [--tooltips <Skill.txt>]
"""
from __future__ import annotations

import argparse
from pathlib import Path

NAMED = [
    "太阴指", "风来吴山", "蹑云逐月", "龙牙", "棒打狗头", "云飞玉皇",
    "阳明指", "横扫六合", "普渡四方", "绛唇珠袖", "破绽产生", "狂龙乱舞",
    "落水打狗", "剑主天地", "商阳指", "屠龙六式",
]
EXCLUDE_SCHOOLS = {"npc", "Quest", "Map", "test", "Carrier", "沙漠风暴"}
BUCKETS = [("melee 4尺", "4"), ("6尺", "6"), ("8尺", "8"), ("10尺", "10"),
           ("20尺", "20"), ("25尺", "25"), ("30尺", "30"), ("50尺", "50")]


def load_report(path: Path):
    lines = path.read_text(encoding="utf-8").splitlines()
    header = lines[0].split("\t")
    idx = {h: i for i, h in enumerate(header)}
    rows = []
    for line in lines[1:]:
        parts = line.split("\t")
        if len(parts) >= len(header):
            rows.append(parts)
    return idx, rows


def row_line(r: list[str], idx: dict[str, int]) -> str:
    name = r[idx["script"]].replace("/", "\\").split("\\")[-1]
    return (f"| {name} | {r[idx['school']]} | {r[idx['min_range_chi']]} | "
            f"{r[idx['max_range_chi']]} | {r[idx['area_radius_chi']]} | "
            f"{r[idx['angle_deg']]} | {r[idx['targets']]} |")


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--report", type=Path, required=True)
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--tooltips", type=Path)
    args = ap.parse_args(argv)

    idx, rows = load_report(args.report)
    out: list[str] = []
    out.append("# JX3 ability range examples (from client scripts)\n")
    out.append("Unit: **1尺 = 64 engine units**.  Angle: `nAngleRange` is in 1/256 "
               "turn, so `256 = 360°` (e.g. `128 = 180°`, `85 ≈ 120°`).\n")
    out.append("| skill script | school | min (尺) | max cast (尺) | area radius (尺) | angle (°) | targets |")
    out.append("|---|---|---|---|---|---|---|")

    seen: set[str] = set()
    for r in rows:
        name = r[idx["script"]].split("\\")[-1]
        if any(k in name for k in NAMED) and r[idx["school"]] not in EXCLUDE_SCHOOLS:
            key = (name, r[idx["max_range_chi"]], r[idx["area_radius_chi"]])
            if key in seen:
                continue
            seen.add(key)
            out.append(row_line(r, idx))

    out.append("")
    out.append("## One example per cast-range bucket\n")
    out.append("| skill script | school | min (尺) | max cast (尺) | area radius (尺) | angle (°) | targets |")
    out.append("|---|---|---|---|---|---|---|")
    for label, value in BUCKETS:
        for r in rows:
            if r[idx["school"]] in EXCLUDE_SCHOOLS:
                continue
            if r[idx["max_range_chi"]] != value:
                continue
            if r[idx["area_radius_chi"]] in ("", "0") and label not in ("melee 4尺", "6尺"):
                continue
            out.append(row_line(r, idx))
            break

    if args.tooltips and args.tooltips.is_file():
        text = args.tooltips.read_bytes().decode("gb18030", errors="replace")
        out.append("")
        out.append("## Player-visible tooltip cross-checks (`ui/Scheme/Case/Skill.txt`)\n")
        wanted = ["太阴指", "风来吴山", "蹑云逐月", "龙牙", "棒打狗头", "云飞玉皇"]
        for line in text.splitlines():
            parts = line.split("\t")
            if len(parts) < 20:
                continue
            name = parts[11] if len(parts) > 11 else ""
            if name in wanted and ("尺" in line or "冲刺" in line or "武器伤害" in line):
                out.append(f"- **{name}** (ID {parts[0]}, L{parts[1]}): {parts[12][:260]}")

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text("\n".join(out) + "\n", encoding="utf-8")
    print(f"{len(out)} lines -> {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
