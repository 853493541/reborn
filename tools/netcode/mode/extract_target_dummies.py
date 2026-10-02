#!/usr/bin/env python3
"""Extract the 木桩 (target dummy) NPC templates used by 主城木桩 / 通用 NPC.

Sources (client PakV4, official PakV4SfxExtract):
  settings\\NpcTemplate\\ZhuChengMuZhuang\\sNpcTemplate.tab
      主城木桩 zone: 初级/中级/高级/极境试炼木桩 (RepresentID 35901-35904) and
      初级/中级/高级/初入江湖木桩 (37023-37026). 试炼教官 rows are excluded
      (dummies only).
  settings\\NpcTemplate\\GongNengTongYongNPC\\sNpcTemplate.tab
      通用 dummies: 初试木桩(50), 江湖木桩(6641), 秘境木桩(6643),
      化境木桩(6644), 极境木桩(6644), 次极境木桩(6644).

The zone table schema: ID, Name, Model, ..., Level, MaxLife, PhysicsShieldBase,
NeutralMagicDefence, ..., RepresentID1..10, ScriptName. The dummy rows carry
the stats the real client spawns (fix: MaxLife 5e8 for 试炼木桩; the PvP heart
buff 木桩心法属性 adds 化劲/御劲 - see docs/pvp/TARGET_DUMMY_RESEARCH.md).

Outputs (default assets/mode/dummy/, gitignored local game data):
  sNpcTemplate_ZhuChengMuZhuang.tab   raw GBK copy
  sNpcTemplate_GongNengTongYongNPC.tab  raw GBK copy
  dummy_index.tsv   dummies joined for the sandbox list
                    (RepresentID, NPCID, Name, Group, Level, MaxLife, MaxMana,
                     Intensity, PhysicsShield, MagicDefence, ScriptName, Zone, Source)

Usage:
  python tools/netcode/mode/extract_target_dummies.py
  python tools/netcode/mode/extract_target_dummies.py --skip-extract
"""
from __future__ import annotations

import argparse
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT))

import pss_assets  # noqa: E402

ZONES = {
    "ZhuChengMuZhuang": r"settings\NpcTemplate\ZhuChengMuZhuang\sNpcTemplate.tab",
    "GongNengTongYongNPC": r"settings\NpcTemplate\GongNengTongYongNPC\sNpcTemplate.tab",
}

INDEX_HEADER = [
    "RepresentID", "NPCID", "Name", "Group", "Level", "MaxLife", "MaxMana",
    "Intensity", "PhysicsShield", "MagicDefence", "ScriptName", "Zone", "Source",
]

GROUP_ORDER = {"江湖木桩": 0, "试炼木桩": 1, "其他木桩": 2}


def extract_raw(out_dir: Path) -> None:
    for zone, logical in ZONES.items():
        found = pss_assets.run_pakv4([logical], work=out_dir / "_work")
        hit = None
        for key, data in found.items():
            if Path(key).name.lower() == "snpctemplate.tab":
                hit = data
        if hit is None:
            raise SystemExit("MISS %s (%s)" % (logical, zone))
        dest = out_dir / ("sNpcTemplate_%s.tab" % zone)
        dest.write_bytes(hit)
        print("HIT  %s -> %s (%d bytes)" % (zone, dest.name, len(hit)))
    shutil.rmtree(out_dir / "_work", ignore_errors=True)


def load_rows(path: Path) -> tuple[list[str], list[list[str]]]:
    text = path.read_bytes().decode("gb18030", errors="replace")
    rows = [r.split("\t") for r in text.splitlines() if r.strip()]
    if not rows:
        return [], []
    return rows[0], rows[1:]


def cell(header: list[str], row: list[str], name: str) -> str:
    for i, h in enumerate(header):
        if h == name:
            return row[i].strip() if i < len(row) else ""
    return ""


def group_of(zone: str, name: str) -> str:
    if "试炼" in name:
        return "试炼木桩"
    if "江湖" in name:
        return "江湖木桩"
    return "其他木桩"


def build_index(out_dir: Path) -> list[list[str]]:
    rows: list[list[str]] = []
    seen: set[int] = set()
    for zone in ZONES:
        header, data = load_rows(out_dir / ("sNpcTemplate_%s.tab" % zone))
        if not header:
            continue
        for r in data:
            name = cell(header, r, "Name")
            if "木桩" not in name or "教官" in name:
                continue
            rid = cell(header, r, "RepresentID1")
            if not rid or rid == "0":
                continue
            try:
                rid_i = int(rid)
            except ValueError:
                continue
            if rid_i in seen:
                continue
            seen.add(rid_i)
            rows.append([
                rid,
                cell(header, r, "ID"),
                name,
                group_of(zone, name),
                cell(header, r, "Level"),
                cell(header, r, "MaxLife"),
                cell(header, r, "MaxMana"),
                cell(header, r, "Intensity"),
                cell(header, r, "PhysicsShieldBase"),
                cell(header, r, "NeutralMagicDefence"),
                cell(header, r, "ScriptName"),
                zone,
                ZONES[zone],
            ])
    rows.sort(key=lambda r: (GROUP_ORDER.get(r[3], 9), int(r[1]) if r[1].isdigit() else 0))
    return rows


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--out-dir", type=Path, default=ROOT / "assets" / "mode" / "dummy")
    ap.add_argument("--skip-extract", action="store_true",
                    help="rebuild the index from the raw copies already in --out-dir")
    args = ap.parse_args(argv)

    out_dir = args.out_dir.resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    if not args.skip_extract:
        extract_raw(out_dir)

    rows = build_index(out_dir)
    if not rows:
        raise SystemExit("no dummy rows found - extraction/output broken")
    index_path = out_dir / "dummy_index.tsv"
    with index_path.open("w", encoding="utf-8", newline="\n") as fh:
        fh.write("\t".join(INDEX_HEADER) + "\n")
        for row in rows:
            fh.write("\t".join(row) + "\n")
    groups: dict[str, int] = {}
    for row in rows:
        groups[row[3]] = groups.get(row[3], 0) + 1
    print("WROTE %s (%d dummies: %s)"
          % (index_path, len(rows), ", ".join("%s=%d" % kv for kv in sorted(groups.items()))))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
