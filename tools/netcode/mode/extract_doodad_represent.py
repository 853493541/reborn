#!/usr/bin/env python3
"""Extract the runtime doodad display table used to render 绝境战场 objects.

Source (client PakV4, official PakV4SfxExtract):
  represent/doodad/doodad.txt   global RepresentID -> model/material/ani/SFX/scale/flags
  settings\\DoodadTemplate.tab   container templates (RepresentID is the join key)

This is the table the real client loads in JX3DoodadRepresent::LoadDoodad
(proof/netcode/disasm/jx3doodadrepresent_load.txt). It is the display side of
every runtime-spawned object: loot boxes, death bags, decoys, gatherables,
mimic/disguise props, event chests, 天原 mechanics props.

Outputs (default assets/mode/doodad/, gitignored local game data):
  doodad.txt            raw GBK copy
  DoodadTemplate.tab    raw GBK copy
  doodad_index.tsv      unique RepresentIDs joined with template rows
                        (沙漠风暴 + 沙漠风暴_寻宝模式; 林海 rows excluded)

Usage:
  python tools/netcode/mode/extract_doodad_represent.py
  python tools/netcode/mode/extract_doodad_represent.py --out-dir assets/mode/doodad
"""
from __future__ import annotations

import argparse
import csv
import io
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT))

import pss_assets  # noqa: E402

CANDIDATES = {
    "doodad.txt": ["represent/doodad/doodad.txt", "represent\\doodad\\doodad.txt"],
    "DoodadTemplate.tab": ["settings\\DoodadTemplate.tab", "settings/DoodadTemplate.tab"],
}
BR_MAPS = {"沙漠风暴", "沙漠风暴_寻宝模式"}
DROP_COLS = ["Drop%d" % i for i in range(1, 11)]

INDEX_HEADER = [
    "RepresentID", "Name", "Templates", "Set", "Drops",
    "Scale", "EffectScale", "MinimapShow", "MinimapShowType",
    "IdleModelFile", "IdleAniFile", "IdleSFXFile",
]
# 林海 (LinHai) asset rows are excluded from the index entirely (requested).
EXCLUDED_SET = "林海"


def extract_raw(out_dir: Path) -> None:
    for name, candidates in CANDIDATES.items():
        hit = None
        for cand in candidates:
            try:
                found = pss_assets.run_pakv4([cand], work=out_dir / "_work")
            except Exception as exc:  # noqa: BLE001
                print("ERR  %s: %s" % (cand, exc))
                continue
            for key, data in found.items():
                if Path(key).name.lower() == name.lower():
                    hit = data
            if hit is not None:
                break
        if hit is None:
            raise SystemExit("MISS %s: tried %s" % (name, candidates))
        (out_dir / name).write_bytes(hit)
        print("HIT  %s -> %d bytes" % (name, len(hit)))
    shutil.rmtree(out_dir / "_work", ignore_errors=True)


def parse_tab(path: Path) -> list[dict[str, str]]:
    text = path.read_bytes().decode("gbk", errors="replace")
    return list(csv.DictReader(io.StringIO(text), delimiter="\t"))


def set_label(drop: str, model: str, name: str) -> str:
    low = ("%s %s %s" % (drop, model, name)).lower()
    if "_lw" in drop or "_skill" in drop or "林海" in low or "lhjj" in low:
        return "林海"
    if "_sea" in low or "海岛" in low or "hdjj" in low:
        return "海岛"
    if "_bl" in low or "白龙" in low or "blk" in low:
        return "白龙"
    if "_xunbao" in low:
        return "寻宝"
    return "龙门"


def build_index(out_dir: Path) -> list[list[str]]:
    rep_text = (out_dir / "doodad.txt").read_bytes().decode("gbk", errors="replace")
    rep_by_id: dict[int, dict[str, str]] = {}
    for row in csv.DictReader(io.StringIO(rep_text), delimiter="\t"):
        try:
            rid = int((row.get("RepresentID") or "").strip() or -1)
        except ValueError:
            continue
        if rid >= 0:
            rep_by_id[rid] = row

    agg: dict[int, dict[str, list[str] | int]] = {}
    for t in parse_tab(out_dir / "DoodadTemplate.tab"):
        if (t.get("MapName") or "").strip() not in BR_MAPS:
            continue
        try:
            rid = int((t.get("RepresentID") or "0").strip() or 0)
        except ValueError:
            rid = 0
        if rid <= 0:
            continue
        name = (t.get("Name") or "").strip()
        drop = next(((t.get(c) or "").strip() for c in DROP_COLS if (t.get(c) or "").strip()), "")
        entry = agg.setdefault(rid, {"names": [], "drops": [], "count": 0})
        assert isinstance(entry["names"], list) and isinstance(entry["drops"], list)
        if name and name not in entry["names"]:
            entry["names"].append(name)
        if drop and drop not in entry["drops"]:
            entry["drops"].append(drop)
        entry["count"] = int(entry["count"]) + 1

    rows: list[list[str]] = []
    missing = 0
    for rid in sorted(agg):
        entry = agg[rid]
        rep = rep_by_id.get(rid)
        if rep is None:
            missing += 1
            continue
        names = entry["names"]
        drops = entry["drops"]
        model = (rep.get("IdleModelFile") or "").strip()
        ani = (rep.get("IdleAniFile") or "").strip()
        sfx = (rep.get("IdleSFXFile") or "").strip()
        label = set_label(drops[0] if drops else "", model, " ".join(names))
        if label == EXCLUDED_SET:
            continue
        rows.append([
            str(rid),
            names[0] if names else "",
            str(entry["count"]),
            label,
            ", ".join(drops[:4]),
            (rep.get("ModelScale") or "").strip(),
            (rep.get("EffectScale") or "").strip(),
            (rep.get("IsMinimapShow") or "").strip(),
            (rep.get("MinimapShowType") or "").strip(),
            model,
            ani,
            sfx,
        ])
    print("join: %d represent ids (%d template rows), %d repr ids without a display row"
          % (len(rows), sum(int(v["count"]) for v in agg.values()), missing))
    return rows


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--out-dir", type=Path, default=ROOT / "assets" / "mode" / "doodad")
    ap.add_argument("--skip-extract", action="store_true",
                    help="rebuild the index from the raw copies already in --out-dir")
    args = ap.parse_args(argv)

    out_dir = args.out_dir.resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    if not args.skip_extract:
        extract_raw(out_dir)

    rows = build_index(out_dir)
    index_path = out_dir / "doodad_index.tsv"
    with index_path.open("w", encoding="utf-8", newline="\n") as fh:
        fh.write("\t".join(INDEX_HEADER) + "\n")
        for row in rows:
            fh.write("\t".join(row) + "\n")
    print("WROTE %s (%d rows)" % (index_path, len(rows)))

    counts: dict[str, int] = {}
    for row in rows:
        counts[row[3]] = counts.get(row[3], 0) + 1
    print("sets: " + ", ".join("%s=%d" % kv for kv in sorted(counts.items())))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
