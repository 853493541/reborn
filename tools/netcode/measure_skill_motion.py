#!/usr/bin/env python3
"""Measure real skill motion (dash) from client data.

Pipeline:
  1. find a .tani in MovieEditor ResourcePack\\Tani.rt (GBK table) by name
  2. extract the tani from client PakV4 via PakV4SfxExtract (pss_assets.run_pakv4)
  3. read its base .ani path from the GATA header (tani.py)
  4. extract the .ani, decode MIN2 (min2.load_min2_stick)
  5. sample root-bone world position per frame -> displacement / speed / duration

Usage:
  python tools/netcode/measure_skill_motion.py --name 太阴指
  python tools/netcode/measure_skill_motion.py --name 玉泉鱼跃 --branch f1
"""
from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

import pss_assets  # noqa: E402
from min2 import load_min2_stick  # noqa: E402
from tani import parse_tani  # noqa: E402

TANI_RT = Path(r"C:\SeasunGame\MovieEditor\ResourcePack\Tani.rt")
WORK = ROOT / "proof" / "netcode" / "skill_motion"


def find_tani_rows(name: str, branch: str = "") -> list[tuple[str, str]]:
    text = TANI_RT.read_bytes().decode("gb18030", errors="replace")
    rows: list[tuple[str, str]] = []
    for line in text.splitlines():
        parts = line.split("\t")
        if len(parts) < 3:
            continue
        display, logical = parts[1], parts[2]
        if name not in display and name not in logical:
            continue
        if branch and not logical.lower().startswith(f"data\\source\\player\\{branch.lower()}\\"):
            continue
        if logical.lower().endswith(".tani"):
            rows.append((display, logical.replace("\\", "\\")))
    return rows


def pick(rows: list[tuple[str, str]]) -> tuple[str, str]:
    for display, logical in rows:
        low = logical.lower()
        if "hd" in low and "皮肤" not in low:
            return display, logical
    return rows[0]


def extract(logical: str) -> tuple[str, bytes] | None:
    found = pss_assets.run_pakv4([logical], work=WORK)
    if not found:
        return None
    key = next(iter(found))
    return key, found[key]


def measure(tani_logical: str) -> dict:
    WORK.mkdir(parents=True, exist_ok=True)
    tani_hit = extract(tani_logical)
    if not tani_hit:
        return {"error": f"tani not extracted: {tani_logical}"}
    key, tani_bytes = tani_hit
    tani_path = WORK / Path(key).name
    tani_path.write_bytes(tani_bytes)
    info = parse_tani(tani_path)
    if not info.ani_path:
        return {"error": "tani has no base ani", "tani": key, "tags": info.other_paths[:10]}
    ani_hit = extract(info.ani_path)
    if not ani_hit:
        return {"error": f"ani not extracted: {info.ani_path}", "tani": key}
    ani_key, ani_bytes = ani_hit
    ani_path = WORK / Path(ani_key).name
    ani_path.write_bytes(ani_bytes)

    clip = load_min2_stick(ani_path)
    frames = []
    root = clip.positions_at(0)[0]
    prev = root
    total = 0.0
    for f in range(clip.frame_count):
        p = clip.positions_at(f)[0]
        d = math.dist(p, prev)
        total += d
        prev = p
        frames.append({
            "frame": f,
            "x": round(p[0], 4),
            "z": round(p[2], 4),
            "dh": round(math.dist((p[0], 0, p[2]), (root[0], 0, root[2])), 4),
        })
        _ = p[1]
    net = math.dist((frames[-1]["x"], frames[-1]["z"]), (frames[0]["x"], frames[0]["z"]))
    duration = clip.frame_count / clip.fps if clip.fps else 0.0
    return {
        "tani": tani_logical,
        "tani_extracted": key,
        "ani": info.ani_path,
        "ani_extracted": ani_key,
        "bones": clip.bone_count,
        "frames": clip.frame_count,
        "fps": round(clip.fps, 3),
        "duration_s": round(duration, 3),
        "root_bone": clip.bone_names[0],
        "root_start": [frames[0]["x"], frames[0]["z"]],
        "root_end": [frames[-1]["x"], frames[-1]["z"]],
        "net_displacement": round(net, 4),
        "path_length": round(total, 4),
        "avg_speed": round(total / duration, 4) if duration else None,
        "curve": frames,
    }


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--name", required=True, help="skill name substring (GBK table)")
    ap.add_argument("--branch", default="", help="restrict to player branch, e.g. f1")
    ap.add_argument("--json", type=Path, help="write full result JSON here")
    args = ap.parse_args(argv)

    rows = find_tani_rows(args.name, args.branch)
    if not rows:
        print(f"no .tani matching {args.name!r}")
        return 2
    print(f"{len(rows)} tani candidates for {args.name!r}:")
    for display, logical in rows[:10]:
        print(f"  - {display}  ->  {logical}")
    display, logical = pick(rows)
    print(f"\nmeasuring: {display}\n  {logical}\n")
    result = measure(logical)
    if "error" in result:
        print("ERROR:", result["error"])
        return 1
    print(json.dumps({k: v for k, v in result.items() if k != "curve"}, ensure_ascii=False, indent=2))
    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"full curve -> {args.json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
